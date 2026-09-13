using System.Diagnostics;

namespace Maverick.Core;

public sealed class ContainmentService
{
    private static readonly HashSet<string> ProtectedProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "system", "idle", "smss", "csrss", "wininit",
            "services", "lsass", "winlogon", "explorer",
            "dwm", "svchost", "spoolsv"
        };

    private readonly Journal journal;
    private readonly QuarantineService quarantine;

    public ContainmentService(Journal journal, QuarantineService quarantine)
    {
        this.journal = journal;
        this.quarantine = quarantine;
    }

    public async Task<object> TerminateThreatProcessAsync(
        int pid,
        string reason,
        IEnumerable<string> evidence,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        using var process = Process.GetProcessById(pid);
        var name = process.ProcessName;

        if (ProtectedProcesses.Contains(name))
            throw new InvalidOperationException(
                $"Maverick will not automatically terminate protected Windows process '{name}'.");

        string? path = null;
        try { path = process.MainModule?.FileName; } catch { }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(token);

        await journal.RecordAsync(
            type: "process_containment",
            severity: "high",
            source: "Maverick.Containment",
            summary: $"Terminated process {name} (PID {pid})",
            details: new { pid, name, path, reason },
            process: name,
            file: path,
            action: "terminate",
            result: "terminated",
            risk: "high",
            evidence: evidence);

        return new
        {
            pid,
            name,
            path,
            status = "terminated"
        };
    }
}
