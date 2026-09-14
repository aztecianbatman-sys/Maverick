namespace Maverick.Core;

public sealed class SelfHealthMonitor : BackgroundService
{
    private readonly IntegrityManifest integrity;
    private readonly Journal journal;
    private readonly CorePaths paths;
    private readonly ServiceConfigurationMonitor serviceConfig;

    public SelfHealthMonitor(
        IntegrityManifest integrity,
        Journal journal,
        CorePaths paths,
        ServiceConfigurationMonitor serviceConfig)
    {
        this.integrity = integrity;
        this.journal = journal;
        this.paths = paths;
        this.serviceConfig = serviceConfig;
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
        try
        {
            ServiceSecurity.ProtectLocalStorage(paths);
        }
        catch (Exception ex)
        {
            await journal.RecordAsync(
                "tamper_detection",
                "high",
                "Maverick.Integrity",
                "Maverick storage ACL verification failed.",
                new { error = ex.Message },
                action: "verify-storage-acl",
                result: "error",
                risk: "high");
        }

        var serviceOkay = await serviceConfig.VerifyAsync(token);
        var changed = await integrity.VerifyAsync(token);

        if (serviceOkay && changed.Count == 0)
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
