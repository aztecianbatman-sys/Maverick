using System.Collections.Concurrent;

namespace Maverick.Core;

public sealed class RansomwareGuard
{
    private readonly Journal journal;
    private readonly ConcurrentQueue<DateTimeOffset> writeEvents = new();
    private readonly ConcurrentQueue<DateTimeOffset> destructiveEvents = new();

    public RansomwareGuard(Journal journal)
    {
        this.journal = journal;
    }

    public async Task ObserveAsync(
        string kind,
        string path,
        CancellationToken token)
    {
        if (kind is "created" or "changed" or "renamed" or "deleted")
        {
            var now = DateTimeOffset.UtcNow;
            writeEvents.Enqueue(now);

            if (kind is "renamed" or "deleted")
                destructiveEvents.Enqueue(now);

            Trim(writeEvents, now, TimeSpan.FromSeconds(10));
            Trim(destructiveEvents, now, TimeSpan.FromSeconds(10));

            var writes = writeEvents.Count;
            var destructive = destructiveEvents.Count;

            if (writes >= 120 && destructive >= 40)
            {
                await journal.RecordAsync(
                    type: "ransomware_signal",
                    severity: "high",
                    source: "Maverick.RansomwareGuard",
                    summary: "High-volume destructive filesystem activity detected.",
                    details: new
                    {
                        writes,
                        destructive,
                        samplePath = path,
                        windowSeconds = 10
                    },
                    file: path,
                    action: "raise-alert",
                    result: "suspicious",
                    risk: "high",
                    evidence: new[]
                    {
                        $"{writes} filesystem events in 10 seconds.",
                        $"{destructive} rename/delete events in 10 seconds.",
                        "User-mode filesystem watcher signal."
                    });

                while (writeEvents.TryDequeue(out _)) { }
                while (destructiveEvents.TryDequeue(out _)) { }
            }
        }

        await Task.CompletedTask;
    }

    private static void Trim(ConcurrentQueue<DateTimeOffset> queue, DateTimeOffset now, TimeSpan window)
    {
        while (queue.TryPeek(out var oldest) && now - oldest > window)
            queue.TryDequeue(out _);
    }
}
