using System.Collections.Concurrent;
using System.Diagnostics;

namespace Maverick.Core;

public sealed class ProcessMonitor : BackgroundService
{
    private readonly Journal journal;
    private readonly ProtectionAnalyzer analyzer;
    private readonly ContainmentService containment;
    private readonly ProcessTracker tracker;
    private readonly ConcurrentDictionary<int, byte> seen = new();

    public ProcessMonitor(
        Journal journal,
        ProtectionAnalyzer analyzer,
        ContainmentService containment,
        ProcessTracker tracker)
    {
        this.journal = journal;
        this.analyzer = analyzer;
        this.containment = containment;
        this.tracker = tracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var process in Process.GetProcesses())
        {
            try { seen.TryAdd(process.Id, 0); }
            finally { process.Dispose(); }
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ObserveSnapshotAsync(stoppingToken);
    }

    private async Task ObserveSnapshotAsync(CancellationToken token)
    {
        var current = Process.GetProcesses();
        var currentIds = new HashSet<int>();

        foreach (var process in current)
        {
            token.ThrowIfCancellationRequested();
            using (process)
            {
                currentIds.Add(process.Id);

                if (!seen.TryAdd(process.Id, 0))
                    continue;

                string? executablePath = null;
                try { executablePath = process.MainModule?.FileName; } catch { }

                if (!string.IsNullOrWhiteSpace(executablePath))
                    tracker.Add(process.Id, name, executablePath);

                var evidence = new List<string>();
                var score = 0;

                if (!string.IsNullOrWhiteSpace(executablePath))
                {
                    var lower = executablePath.ToLowerInvariant();
                    if (lower.Contains("\\downloads\\"))
                    {
                        evidence.Add("Process image is under a Downloads directory.");
                        score += 10;
                    }

                    if (lower.Contains("\\appdata\\local\\temp\\"))
                    {
                        evidence.Add("Process image is under a temporary directory.");
                        score += 15;
                    }
                }

                var name = process.ProcessName;
                if (name.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("wscript", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("cscript", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("mshta", StringComparison.OrdinalIgnoreCase))
                {
                    evidence.Add($"Script/interpreter process: {name}.");
                    score += 5;
                }

                var risk = score >= 20 ? "Medium" : "Low";
                if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
                {
                    try
                    {
                        var verdict = await analyzer.AnalyzeFileAsync(executablePath);

                        if (verdict.Verdict == "Threat" && verdict.Action == "Quarantine")
                        {
                            await journal.RecordAsync(
                                "process_threat",
                                "high",
                                "Maverick.ProcessMonitor",
                                $"Threat process detected: {name}",
                                new { pid = process.Id, executablePath, verdict },
                                process: name,
                                file: executablePath,
                                action: "contain",
                                result: "threat",
                                risk: "high",
                                evidence: verdict.Evidence);

                            await containment.TerminateThreatProcessAsync(
                                process.Id,
                                "Executable image received a deterministic Threat verdict.",
                                verdict.Evidence,
                                token);
                        }
                    }
                    catch (Exception ex)
                    {
                        await journal.RecordAsync(
                            "process_analysis_error",
                            "warning",
                            "Maverick.ProcessMonitor",
                            $"Could not analyze process image for {name}.",
                            new { pid = process.Id, executablePath, error = ex.Message },
                            process: name,
                            file: executablePath,
                            action: "analyze",
                            result: "error",
                            risk: "unknown");
                    }
                }

                await journal.RecordAsync(
                    type: "process_start",
                    severity: risk == "Medium" ? "warning" : "info",
                    source: "Maverick.ProcessMonitor",
                    summary: $"Process started: {name}",
                    details: new
                    {
                        pid = process.Id,
                        name,
                        executablePath,
                        score
                    },
                    process: name,
                    file: executablePath,
                    action: "observe",
                    result: "observed",
                    risk: risk,
                    evidence: evidence);
            }
        }

        foreach (var id in seen.Keys)
        {
            if (!currentIds.Contains(id))
            {
                seen.TryRemove(id, out _);
                tracker.Remove(id);
            }
        }
    }
}
