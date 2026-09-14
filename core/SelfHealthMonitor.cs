namespace Maverick.Core;

public sealed class SelfHealthMonitor : BackgroundService
{
    private readonly IntegrityManifest integrity;
    private readonly Journal journal;

    public SelfHealthMonitor(IntegrityManifest integrity, Journal journal)
    {
        this.integrity = integrity;
        this.journal = journal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CheckAsync(stoppingToken);
    }

    private async Task CheckAsync(CancellationToken token)
    {
        var changed = await integrity.VerifyAsync(token);

        if (changed.Count == 0)
        {
            await journal.RecordAsync(
                "self_health",
                "info",
                "Maverick.Integrity",
                "Maverick integrity check completed.",
                new { checkedAtUtc = DateTimeOffset.UtcNow },
                action: "verify-integrity",
                result: "ok",
                risk: "info");
        }
    }
}
