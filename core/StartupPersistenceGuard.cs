using Microsoft.Win32;

namespace Maverick.Core;

public sealed class StartupPersistenceGuard
{
    private readonly Journal journal;
    private readonly ProtectionAnalyzer analyzer;

    public StartupPersistenceGuard(Journal journal, ProtectionAnalyzer analyzer)
    {
        this.journal = journal;
        this.analyzer = analyzer;
    }

    public async Task<int> InspectAndRemediateAsync(CancellationToken token)
    {
        var removed = 0;

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var keyPath in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
            })
            {
                using var key = hive.OpenSubKey(keyPath, writable: true);
                if (key is null) continue;

                foreach (var name in key.GetValueNames())
                {
                    token.ThrowIfCancellationRequested();
                    var value = key.GetValue(name)?.ToString();
                    var target = ExtractLikelyPath(value);
                    if (target is null || !File.Exists(target)) continue;

                    var verdict = await analyzer.AnalyzeFileAsync(target);
                    if (verdict.Verdict != "Threat" || verdict.Action != "Quarantine")
                        continue;

                    key.DeleteValue(name, throwOnMissingValue: false);
                    removed++;

                    await journal.RecordAsync(
                        type: "persistence_remediation",
                        severity: "high",
                        source: "Maverick.Persistence",
                        summary: $"Removed malicious startup entry: {name}",
                        details: new { hive = hive.Name, keyPath, name, target, verdict },
                        file: target,
                        action: "remove-startup-entry",
                        result: "removed",
                        risk: verdict.Risk,
                        evidence: verdict.Evidence);
                }
            }
        }

        return removed;
    }

    private static string? ExtractLikelyPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Trim();

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0) return text[..(exe + 4)];

        return File.Exists(text) ? text : null;
    }
}

public sealed class PersistenceMonitor : BackgroundService
{
    private readonly StartupPersistenceGuard guard;

    public PersistenceMonitor(StartupPersistenceGuard guard)
    {
        this.guard = guard;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await guard.InspectAndRemediateAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await guard.InspectAndRemediateAsync(stoppingToken);
            }
            catch (Exception)
            {
                // Keep the monitor alive; individual persistence problems are journaled by later runs.
            }
        }
    }
}
