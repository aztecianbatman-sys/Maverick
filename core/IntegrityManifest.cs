using System.Security.Cryptography;
using System.Text.Json;

namespace Maverick.Core;

public sealed record IntegrityEntry(string Path, string Sha256);

public sealed class IntegrityManifest
{
    private readonly CorePaths paths;
    private readonly Journal journal;

    public IntegrityManifest(CorePaths paths, Journal journal)
    {
        this.paths = paths;
        this.journal = journal;
    }

    public async Task<IReadOnlyList<string>> VerifyAsync(CancellationToken token)
    {
        var manifestPath = paths.IntegrityManifestPath;
        if (!File.Exists(manifestPath))
            return Array.Empty<string>();

        List<IntegrityEntry>? entries;

        try
        {
            entries = JsonSerializer.Deserialize<List<IntegrityEntry>>(
                await File.ReadAllTextAsync(manifestPath, token));
        }
        catch (Exception ex)
        {
            await journal.RecordAsync(
                "tamper_detection",
                "high",
                "Maverick.Integrity",
                "Integrity manifest could not be read.",
                new { error = ex.Message },
                action: "verify-integrity",
                result: "error",
                risk: "high");
            return new[] { manifestPath };
        }

        var changed = new List<string>();

        foreach (var entry in entries ?? [])
        {
            token.ThrowIfCancellationRequested();

            if (!File.Exists(entry.Path))
            {
                changed.Add(entry.Path);
                continue;
            }

            try
            {
                await using var stream = new FileStream(
                    entry.Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    1024 * 1024,
                    useAsync: true);

                using var sha = SHA256.Create();
                var digest = Convert.ToHexString(
                    await sha.ComputeHashAsync(stream)).ToLowerInvariant();

                if (!digest.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    changed.Add(entry.Path);
            }
            catch
            {
                changed.Add(entry.Path);
            }
        }

        if (changed.Count > 0)
        {
            await journal.RecordAsync(
                "tamper_detection",
                "high",
                "Maverick.Integrity",
                $"Maverick integrity mismatch detected for {changed.Count} file(s).",
                new { files = changed },
                action: "verify-integrity",
                result: "changed",
                risk: "high",
                evidence: changed.Select(x => $"integrity-mismatch:{x}"));
        }

        return changed;
    }
}
