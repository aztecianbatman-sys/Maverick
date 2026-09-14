using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Maverick.Core;

public static class Program
{
    public static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
            options.ServiceName = "Maverick Core");

        builder.Services.AddSingleton<CorePaths>();
        builder.Services.AddSingleton<IntegrityManifest>();
        builder.Services.AddSingleton<Journal>();
        builder.Services.AddSingleton<FileInspector>();
        builder.Services.AddSingleton<SystemInventory>();
        builder.Services.AddSingleton<ThreatKnowledgeCatalog>();
        builder.Services.AddSingleton<KnownBadHashStore>();
        builder.Services.AddSingleton<ThreatIntelService>();
        builder.Services.AddSingleton<AmsiScanner>();
        builder.Services.AddSingleton<ProtectionAnalyzer>();
        builder.Services.AddSingleton<QuarantineService>();
        builder.Services.AddSingleton<ProtectionService>();
        builder.Services.AddSingleton<ContainmentService>();
        builder.Services.AddSingleton<DownloadOriginReader>();
        builder.Services.AddSingleton<ProcessTracker>();
        builder.Services.AddSingleton<RansomwareGuard>();
        builder.Services.AddSingleton<StartupPersistenceGuard>();
        builder.Services.AddSingleton<ScanService>();
        builder.Services.AddSingleton<ServiceConfigurationMonitor>();

        builder.Services.AddSingleton<SecurityMonitor>();
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<SecurityMonitor>());

        builder.Services.AddHostedService<ProcessMonitor>();
        builder.Services.AddHostedService<SelfHealthMonitor>();
        builder.Services.AddHostedService<PersistenceMonitor>();
        builder.Services.AddHostedService<PipeServer>();

        return builder.Build().RunAsync();
    }
}
