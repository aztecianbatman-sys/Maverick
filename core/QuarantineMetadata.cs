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
            });

        return new
        {
            id,
            originalPath = info.FullName,
            sha256 = sha,
            sizeBytes = info.Exists ? info.Length : null,
            quarantineDirectory = paths.QuarantineDirectory,
            status = "metadata-only"
        };
    }
}
