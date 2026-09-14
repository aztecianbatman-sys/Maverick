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

    public async Task<int> ImportAsync(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("Threat hash feed not found.", jsonPath);

        var json = await File.ReadAllTextAsync(jsonPath);
        var entries = JsonSerializer.Deserialize<List<KnownBadHash>>(json)
            ?? throw new InvalidOperationException("Threat hash feed is not a JSON array.");

        var normalized = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.Sha256))
            .Select(x => x with { Sha256 = x.Sha256.Trim().ToLowerInvariant() })
            .Where(x => x.Sha256.Length == 64 && x.Sha256.All(Uri.IsHexDigit))
            .ToList();

        foreach (var entry in normalized)
            hashes[entry.Sha256] = entry;

        var path = Path.Combine(paths.RootDirectory, "known-bad-hashes.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                hashes.Values.OrderBy(x => x.Sha256),
                new JsonSerializerOptions { WriteIndented = true }));

        await journal.RecordAsync(
            "threat_intel_import",
            "info",
            "Maverick.ThreatIntel",
            $"Imported {normalized.Count} validated hash entries.",
            new { source = jsonPath, imported = normalized.Count },
            action: "import-hash-catalog",
            result: "completed",
            risk: "info");

        return normalized.Count;
    }

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
