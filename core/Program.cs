using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Maverick.Core;

public static class Program
{
    public static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options => options.ServiceName = "Maverick Core");
        builder.Services.AddSingleton<CorePaths>();
        builder.Services.AddSingleton<Journal>();
        builder.Services.AddSingleton<FileInspector>();
        builder.Services.AddSingleton<SystemInventory>();
        builder.Services.AddSingleton<QuarantineMetadata>();
        builder.Services.AddSingleton<ScanService>();
        builder.Services.AddHostedService<SecurityMonitor>();
        builder.Services.AddHostedService<PipeServer>();
        return builder.Build().RunAsync();
    }
}
