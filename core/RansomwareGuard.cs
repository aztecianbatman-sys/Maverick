using System.Collections.Concurrent;

namespace Maverick.Core;

public sealed class RansomwareGuard
{
    private readonly Journal journal;
    private readonly ContainmentService containment;
    private readonly ProcessTracker processTracker;
    private readonly ConcurrentQueue<DateTimeOffset> writeEvents = new();
    private readonly ConcurrentQueue<DateTimeOffset> destructiveEvents = new();
    private readonly SemaphoreSlim triggerGate = new(1, 1);

    public RansomwareGuard(
        Journal journal,
        ContainmentService containment,
        ProcessTracker processTracker)
    {
        this.journal = journal;
        this.containment = containment;
        this.processTracker = processTracker;
    }

    public async Task ObserveAsync(string kind, string path, CancellationToken token)
    {
        if (kind is not ("created" or "changed" or "renamed" or "deleted"))
            return;

        var now = DateTimeOffset.UtcNow;
        writeEvents.Enqueue(now);

        if (kind is "renamed" or "deleted")
            destructiveEvents.Enqueue(now);

        Trim(writeEvents, now, TimeSpan.FromSeconds(10));
        Trim(destructiveEvents, now, TimeSpan.FromSeconds(10));

        var writes = writeEvents.Count;
        var destructive = destructiveEvents.Count;

        if (writes < 250 || destructive < 100)
            return;

        if (!await triggerGate.WaitAsync(0, token))
            return;

        try
        {
            var evidence = new[]
            {
                $"{writes} filesystem events observed in 10 seconds.",
                $"{destructive} rename/delete events observed in 10 seconds.",
                "High-volume destructive activity exceeds Maverick's containment threshold."
            };

            await journal.RecordAsync(
                type: "ransomware_signal",
                severity: "high",
                source: "Maverick.RansomwareGuard",
                summary: "High-volume destructive filesystem activity detected; containment initiated.",
                details: new
                {
                    writes,
                    destructive,
                    samplePath = path,
                    windowSeconds = 10
                },
                file: path,
                action: "contain",
                result: "triggered",
                risk: "high",
                evidence: evidence);

            var candidates = processTracker.Recent(TimeSpan.FromSeconds(15))
                .Where(p => !string.IsNullOrWhiteSpace(p.ExecutablePath))
                .Where(IsUserWritablePath)
                .Take(8)
                .ToList();

            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await containment.TerminateThreatProcessAsync(
                        candidate.Pid,
                        "High-volume destructive filesystem activity correlated with a recently started process.",
                        evidence,
                        token);
                }
                catch (Exception ex)
                {
                    await journal.RecordAsync(
                        "process_containment_error",
                        "warning",
                        "Maverick.RansomwareGuard",
                        $"Could not contain PID {candidate.Pid}.",
                        new { candidate, error = ex.Message },
                        process: candidate.Name,
                        file: candidate.ExecutablePath,
                        action: "terminate",
                        result: "error",
                        risk: "high");
                }
            }

            writeEvents.Clear();
            destructiveEvents.Clear();
        }
        finally
        {
            triggerGate.Release();
        }
    }

    private static bool IsUserWritablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var lower = Path.GetFullPath(path).ToLowerInvariant();
        return lower.Contains("\\appdata\\local\\temp\\") ||
               lower.Contains("\\downloads\\") ||
               lower.Contains("\\desktop\\");
    }

    private static void Trim(
        ConcurrentQueue<DateTimeOffset> queue,
        DateTimeOffset now,
        TimeSpan window)
    {
        while (queue.TryPeek(out var oldest) && now - oldest > window)
            queue.TryDequeue(out _);
    }
}
