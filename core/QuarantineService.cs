namespace Maverick.Core;

public sealed class QuarantineService
{
    private readonly CorePaths paths;
    private readonly Journal journal;
    private readonly FileInspector inspector;

    public QuarantineService(CorePaths paths, Journal journal, FileInspector inspector)
    {
        this.paths = paths;
        this.journal = journal;
        this.inspector = inspector;
    }

    public async Task<object> QuarantineAsync(
        string originalPath,
        string reason,
        IEnumerable<string> evidence)
    {
        var source = Path.GetFullPath(originalPath);

        if (!File.Exists(source))
            throw new FileNotFoundException("File is no longer present.", source);

        var id = Guid.NewGuid().ToString("N");
        var destination = Path.Combine(
            paths.QuarantineDirectory,
            id + ".quarantined");

        var info = new FileInfo(source);
        var hash = await inspector.HashAsync(source);

        Directory.CreateDirectory(paths.QuarantineDirectory);
        File.Move(source, destination, overwrite: false);

        try
        {
            File.SetAttributes(
                destination,
                File.GetAttributes(destination) | FileAttributes.ReadOnly);
        }
        catch
        {
            // Read-only is defense-in-depth; quarantine already succeeded.
        }

        await journal.RecordQuarantineAsync(
            id,
            source,
            hash,
            info.Length,
            "quarantined");

        await journal.RecordAsync(
            type: "quarantine",
            severity: "high",
            source: "Maverick.Core",
            summary: $"Quarantined {info.Name}",
            details: new
            {
                id,
                originalPath = source,
                quarantinePath = destination,
                reason
            },
            file: source,
            action: "quarantine",
            result: "quarantined",
            risk: "high",
            evidence: evidence);

        return new
        {
            id,
            originalPath = source,
            quarantinePath = destination,
            sha256 = hash,
            reason,
            status = "quarantined"
        };
    }
}


    public Task<object> ListAsync()
    {
        var items = Directory.EnumerateFiles(paths.QuarantineDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTimeUtc)
            .Select(info => new
            {
                path = info.FullName,
                name = info.Name,
                sizeBytes = info.Length,
                createdUtc = info.CreationTimeUtc
            })
            .ToList();

        return Task.FromResult<object>(items);
    }

    public async Task<object> DeleteQuarantinedAsync(string path)
    {
        var quarantineRoot = Path.GetFullPath(paths.QuarantineDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var target = Path.GetFullPath(path);

        if (!target.StartsWith(quarantineRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only files inside Maverick quarantine may be deleted.");

        if (!File.Exists(target))
            throw new FileNotFoundException("Quarantine file not found.", target);

        File.Delete(target);

        await journal.RecordAsync(
            "quarantine_delete",
            "high",
            "Maverick.Core",
            $"Deleted quarantined item {Path.GetFileName(target)}",
            new { quarantinePath = target },
            file: target,
            action: "delete-quarantined",
            result: "deleted",
            risk: "high");

        return new { quarantinePath = target, status = "deleted" };
    }
}
