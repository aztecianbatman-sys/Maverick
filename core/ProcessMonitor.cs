using System.Collections.Concurrent;
using System.Diagnostics;

namespace Maverick.Core;

public sealed class ProcessMonitor : BackgroundService
{
    private readonly Journal journal;
    private readonly ConcurrentDictionary<int, byte> seen = new();

    public ProcessMonitor(Journal journal)
    {
        this.journal = journal;
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
                seen.TryRemove(id, out _);
        }
    }
}
