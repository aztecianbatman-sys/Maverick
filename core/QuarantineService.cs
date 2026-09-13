namespace Maverick.Core;

public sealed class QuarantineService
{
    private readonly CorePaths paths;
    private readonly Journal journal;

    public QuarantineService(CorePaths paths, Journal journal)
    {
        this.paths = paths;
        this.journal = journal;
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
        var hash = await new FileInspector().HashAsync(source);

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
