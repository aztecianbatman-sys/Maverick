using System.Text.Json;

namespace Maverick.Core;

public sealed class SecurityMonitor : BackgroundService
{
    private readonly Journal journal;
    private readonly CorePaths paths;
    private readonly ProtectionService protection;
    private readonly List<FileSystemWatcher> watchers = new();
    private readonly object sync = new();

    public SecurityMonitor(Journal journal, CorePaths paths, ProtectionService protection)
    {
        this.journal = journal;
        this.paths = paths;
        this.protection = protection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadConfiguredWatchesAsync(stoppingToken);
        await stoppingToken.WaitHandle.WaitOneAsync();
    }

    public async Task ConfigureAsync(IEnumerable<string> requestedPaths)
    {
        var pathsToWatch = requestedPaths
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        lock (sync)
        {
            foreach (var watcher in watchers)
                watcher.Dispose();

            watchers.Clear();

            foreach (var path in pathsToWatch)
            {
                var watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter =
                        NotifyFilters.FileName |
                        NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite |
                        NotifyFilters.Size
                };

                watcher.Created += (_, e) =>
                    _ = HandleChangedFileAsync("created", e.FullPath);

                watcher.Changed += (_, e) =>
                    _ = HandleChangedFileAsync("changed", e.FullPath);

                watcher.Deleted += (_, e) =>
                    _ = RecordFileEvent("deleted", e.FullPath);

                watcher.Renamed += (_, e) =>
                    _ = RecordRenameEvent(e);

                watcher.Error += (_, e) =>
                    _ = journal.RecordAsync(
                        "monitor_error",
                        "warning",
                        "FileSystemWatcher",
                        e.GetException()?.Message ?? "Watcher error",
                        new { path });

                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
        }

        var config = JsonSerializer.Serialize(new { paths = pathsToWatch });
        await File.WriteAllTextAsync(paths.ConfigPath, config);

        await journal.RecordAsync(
            "monitor_configured",
            "info",
            "Maverick.Core",
            $"Monitoring {pathsToWatch.Count} directories",
            new { paths = pathsToWatch });
    }

    private async Task LoadConfiguredWatchesAsync(CancellationToken token)
    {
        if (!File.Exists(paths.ConfigPath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(paths.ConfigPath, token);
            var config = JsonSerializer.Deserialize<MonitorConfig>(json);

            if (config?.Paths is not null)
                await ConfigureAsync(config.Paths);
        }
        catch (Exception ex)
        {
            await journal.RecordAsync(
                "monitor_config_error",
                "warning",
                "Maverick.Core",
                "Could not load monitor configuration.",
                new { error = ex.Message });
        }
    }

    private async Task HandleChangedFileAsync(string kind, string path)
    {
        var fullPath = Path.GetFullPath(path);
        await RecordFileEvent(kind, fullPath);

        if (!File.Exists(fullPath))
            return;

        try
        {
            await Task.Delay(250);
            await protection.AnalyzeFileAsync(
                fullPath,
                allowQuarantine: true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            await journal.RecordAsync(
                "protection_analysis_error",
                "warning",
                "Maverick.Protection",
                $"Could not analyze {fullPath}",
                new { path = fullPath, error = ex.Message },
                file: fullPath,
                action: "analyze",
                result: "error",
                risk: "unknown");
        }
    }

    private Task RecordFileEvent(string kind, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var file = Path.GetFileName(fullPath);
        return journal.RecordAsync(
            type: "filesystem",
            severity: "info",
            source: "FileSystemWatcher",
            summary: $"{kind}: {fullPath}",
            details: new { kind, path = fullPath },
            file: fullPath,
            action: kind,
            result: kind == "deleted" ? "removed" : "observed",
            risk: "unknown");
    }

    private Task RecordRenameEvent(RenamedEventArgs e)
    {
        var oldPath = Path.GetFullPath(e.OldFullPath);
        var newPath = Path.GetFullPath(e.FullPath);
        return journal.RecordAsync(
            type: "filesystem",
            severity: "info",
            source: "FileSystemWatcher",
            summary: $"renamed: {oldPath} -> {newPath}",
            details: new
            {
                kind = "renamed",
                oldPath,
                path = newPath
            },
            file: newPath,
            action: "renamed",
            result: "observed",
            risk: "unknown",
            evidence: new[] { $"old-path:{oldPath}" });
    }

    private sealed record MonitorConfig(List<string> Paths);
}

file static class CancellationTokenWaitHandleExtensions
{
    public static Task WaitOneAsync(this WaitHandle handle)
    {
        var tcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        ThreadPool.RegisterWaitForSingleObject(
            handle,
            (_, _) => tcs.TrySetResult(),
            null,
            -1,
            executeOnlyOnce: true);

        return tcs.Task;
    }
}
