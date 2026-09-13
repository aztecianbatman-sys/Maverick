namespace Maverick.Core;

public sealed class ScanService
{
    private readonly FileInspector files;
    private readonly Journal journal;

    public ScanService(FileInspector files, Journal journal)
    {
        this.files = files;
        this.journal = journal;
    }

    public async Task<object> ScanAsync(string path, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(path);

        if (File.Exists(root))
            return await InspectOneAsync(root, cancellationToken);

        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);

        long inspected = 0;
        long failed = 0;
        var started = DateTimeOffset.UtcNow;

        foreach (var file in EnumerateFilesSafe(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await files.HashAsync(file);
                inspected++;
            }
            catch
            {
                failed++;
            }
        }

        var completed = DateTimeOffset.UtcNow;

        await journal.RecordAsync(
            "scan",
            "info",
            "Maverick.Core",
            $"Inventory scan completed for {root}",
            new
            {
                root,
                inspected,
                failed,
                startedUtc = started,
                completedUtc = completed
            });

        return new
        {
            root,
            inspected,
            failed,
            startedUtc = started,
            completedUtc = completed,
            verdict = "inventory-only"
        };
    }

    private async Task<object> InspectOneAsync(
        string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await files.InspectAsync(path, true);

        await journal.RecordAsync(
            "file_inspection",
            "info",
            "Maverick.Core",
            $"Inspected {Path.GetFileName(path)}",
            result);

        return new
        {
            result,
            verdict = "inventory-only"
        };
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };

        return Directory.EnumerateFiles(root, "*", options);
    }
}
