namespace Maverick.Core;

public sealed class QuarantineMetadata
{
    private readonly CorePaths paths;
    private readonly Journal journal;

    public QuarantineMetadata(CorePaths paths, Journal journal)
    {
        this.paths = paths;
        this.journal = journal;
    }

    public async Task<object> RegisterAsync(string originalPath)
    {
        var fullPath = Path.GetFullPath(originalPath);
        var info = new FileInfo(fullPath);

        string? sha = null;
        if (info.Exists)
        {
            await using var stream = info.Open(
                FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            using var crypto = System.Security.Cryptography.SHA256.Create();
            sha = Convert.ToHexString(
                await crypto.ComputeHashAsync(stream)).ToLowerInvariant();
        }

        var id = Guid.NewGuid().ToString("N");

        await journal.RecordQuarantineAsync(
            id, info.FullName, sha,
            info.Exists ? info.Length : null,
            "metadata-only");

        await journal.RecordAsync(
            "quarantine_metadata",
            "info",
            "Maverick.Core",
            $"Quarantine metadata registered for {info.Name}",
            new
            {
                id,
                originalPath = info.FullName,
                sha256 = sha,
                status = "metadata-only",
                quarantineDirectory = paths.QuarantineDirectory
            },
            file: info.FullName,
            action: "register-quarantine-metadata",
            result: "recorded",
            risk: "unknown",
            evidence: sha is null ? null : new[] { $"sha256:{sha}" });

        return new
        {
            id,
            originalPath = info.FullName,
            sha256 = sha,
            sizeBytes = info.Exists ? (long?)info.Length : null,
            quarantineDirectory = paths.QuarantineDirectory,
            status = "metadata-only"
        };
    }

    public Task<object> ListAsync()
    {
        var items = Directory.EnumerateFiles(
                paths.QuarantineDirectory,
                "*",
                SearchOption.TopDirectoryOnly)
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

        File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
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
