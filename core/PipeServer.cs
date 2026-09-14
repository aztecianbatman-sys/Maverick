using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Maverick.Core;

public sealed class PipeServer : BackgroundService
{
    private const string PipeName = "MaverickCore";

    private readonly FileInspector files;
    private readonly SystemInventory inventory;
    private readonly Journal journal;
    private readonly QuarantineMetadata quarantine;
    private readonly ScanService scans;
    private readonly SecurityMonitor monitor;
    private readonly ProtectionService protection;
    private readonly KnownBadHashStore threatIntelHashes;

    public PipeServer(
        FileInspector files,
        SystemInventory inventory,
        Journal journal,
        QuarantineMetadata quarantine,
        ScanService scans,
        SecurityMonitor monitor,
        ProtectionService protection,
        KnownBadHashStore threatIntelHashes)
    {
        this.files = files;
        this.inventory = inventory;
        this.journal = journal;
        this.quarantine = quarantine;
        this.scans = scans;
        this.monitor = monitor;
        this.protection = protection;
        this.threatIntelHashes = threatIntelHashes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var server = CreateServer();

            try
            {
                await server.WaitForConnectionAsync(stoppingToken);
                await HandleClientAsync(server, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                await journal.RecordAsync(
                    "ipc_error",
                    "warning",
                    "Maverick.Core",
                    "IPC request failed.",
                    new { error = ex.Message });
            }
        }
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = new PipeSecurity();

        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite |
                PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            64 * 1024,
            64 * 1024);
    }

    private async Task HandleClientAsync(
        Stream stream, CancellationToken token)
    {
        using var reader =
            new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        using var writer =
            new StreamWriter(
                stream,
                new UTF8Encoding(false),
                leaveOpen: true)
            {
                AutoFlush = true
            };

        var line = await reader.ReadLineAsync(token);

        if (string.IsNullOrWhiteSpace(line))
            return;

        using var request = JsonDocument.Parse(line);
        var root = request.RootElement;

        var command =
            root.TryGetProperty("command", out var commandElement)
                ? commandElement.GetString()
                : null;

        try
        {
            object result = command switch
            {
                "status" => new
                {
                    service = "Maverick Core",
                    version = "0.5.2",
                    mode = "user-mode",
                    capabilities = new[]
                    {
                        "file-inspection",
                        "sha256",
                        "processes",
                        "startup",
                        "services",
                        "scheduled-tasks",
                        "filesystem-monitor",
                        "file-protection",
                        "process-monitor",
                        "threat-intelligence",
                        "amsi-script-scanning",
                        "journal",
                        "journal-query",
                        "self-defense",
                        "quarantine-metadata",
                        "inventory-scan"
                    }
                },

                "file.inspect" => await files.InspectAsync(
                    root.GetProperty("path").GetString() ?? "",
                    root.TryGetProperty("hash", out var hash)
                        ? hash.GetBoolean()
                        : true),

                "processes.list" => inventory.ListProcesses(),
                "services.list" => inventory.ListServices(),
                "tasks.list" => await inventory.ListScheduledTasks(),
                "startup.list" => inventory.ListStartupLocations(),

                "journal.recent" => await journal.RecentAsync(
                    root.TryGetProperty("limit", out var limit)
                        ? limit.GetInt32()
                        : 100),

                "journal.today" => await journal.TodayAsync(
                    root.TryGetProperty("limit", out var todayLimit)
                        ? todayLimit.GetInt32()
                        : 200),

                "journal.search" => await journal.SearchAsync(
                    root.TryGetProperty("type", out var type)
                        ? type.GetString()
                        : null,
                    root.TryGetProperty("risk", out var risk)
                        ? risk.GetString()
                        : null,
                    root.TryGetProperty("limit", out var searchLimit)
                        ? searchLimit.GetInt32()
                        : 200),

                "scan" => await scans.ScanAsync(
                    root.GetProperty("path").GetString() ?? "",
                    token),

                "quarantine.register" => await quarantine.RegisterAsync(
                    root.GetProperty("path").GetString() ?? ""),

                "quarantine.list" => await quarantine.ListAsync(),

                "quarantine.delete" => await quarantine.DeleteQuarantinedAsync(
                    root.GetProperty("path").GetString() ?? ""),

                "monitor.configure" => await ConfigureMonitorAsync(root),

                "protection.status" => new
                {
                    mode = "user-mode",
                    verdicts = new[] { "Safe", "Suspicious", "Threat" },
                    automaticContainment = "Deterministic local intelligence and confirmed high-confidence signals"
                },

                "threatintel.import" => await ImportThreatIntelAsync(root),

                "file.analyze" => await protection.AnalyzeFileAsync(
                    root.GetProperty("path").GetString() ?? "",
                    root.TryGetProperty("quarantine", out var quarantine)
                        ? quarantine.GetBoolean()
                        : false,
                    token),

                _ => throw new InvalidOperationException(
                    $"Unknown command: {command}")
            };

            await WriteResponse(writer, true, result, null);
        }
        catch (Exception ex)
        {
            await WriteResponse(writer, false, null, ex.Message);
        }
    }

    private async Task<object> ImportThreatIntelAsync(JsonElement root)
    {
        var path = root.GetProperty("path").GetString() ?? "";
        var count = await threatIntelHashes.ImportAsync(path);
        return new { imported = count };
    }

    private async Task<object> ConfigureMonitorAsync(JsonElement root)
    {
        if (!root.TryGetProperty("paths", out var pathsElement) ||
            pathsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "paths must be an array.");
        }

        var paths = pathsElement
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>();

        await monitor.ConfigureAsync(paths);

        return new { ok = true };
    }

    private static Task WriteResponse(
        StreamWriter writer,
        bool ok,
        object? result,
        string? error)
    {
        var payload = JsonSerializer.Serialize(
            new { ok, result, error });

        return writer.WriteLineAsync(payload);
    }
}
