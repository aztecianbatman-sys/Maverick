using System.Text.Json;

namespace Maverick.Core;

public sealed record KnownBadHash(string Sha256, string Name, string Family, string Source, string Action);

public sealed class KnownBadHashStore
{
    private readonly CorePaths paths;
    private readonly Journal journal;
    private readonly Dictionary<string, KnownBadHash> hashes = new(StringComparer.OrdinalIgnoreCase);

    public KnownBadHashStore(CorePaths paths, Journal journal)
    {
        this.paths = paths;
        this.journal = journal;
        LoadAsync().GetAwaiter().GetResult();
    }

    public bool TryGet(string sha256, out KnownBadHash? match) =>
        hashes.TryGetValue(sha256, out match);

    private async Task LoadAsync()
    {
        var path = Path.Combine(paths.RootDirectory, "known-bad-hashes.json");

        if (!File.Exists(path))
        {
            var seed = new[]
            {
                new KnownBadHash(
                    "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f",
                    "EICAR test file",
                    "EICAR",
                    "EICAR test signature",
                    "quarantine")
            };

            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(seed, new JsonSerializerOptions { WriteIndented = true }));
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var entries = JsonSerializer.Deserialize<List<KnownBadHash>>(json) ?? [];

            hashes.Clear();

            foreach (var entry in entries)
            {
                var normalized = entry.Sha256.Trim().ToLowerInvariant();
                if (normalized.Length == 64 && normalized.All(Uri.IsHexDigit))
                    hashes[normalized] = entry;
            }
        }
        catch (Exception ex)
        {
            await journal.RecordAsync(
                "threat_intel_error",
                "warning",
                "Maverick.ThreatIntel",
                "Could not load local known-bad hash catalog.",
                new { error = ex.Message },
                action: "load-known-bad",
                result: "error",
                risk: "unknown");
        }
    }
}
