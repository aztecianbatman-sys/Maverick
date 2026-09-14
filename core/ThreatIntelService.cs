namespace Maverick.Core;

public sealed class ThreatIntelService
{
    private readonly FileInspector inspector;
    private readonly KnownBadHashStore hashes;
    private readonly ThreatKnowledgeCatalog knowledge;
    private readonly Journal journal;

    public ThreatIntelService(
        FileInspector inspector,
        KnownBadHashStore hashes,
        ThreatKnowledgeCatalog knowledge,
        Journal journal)
    {
        this.inspector = inspector;
        this.hashes = hashes;
        this.knowledge = knowledge;
        this.journal = journal;
    }

    public async Task<(KnownBadHash? HashMatch, IReadOnlyList<string> FamilyMatches, string? Sha256)> AnalyzeAsync(
        string path,
        IEnumerable<string> signals,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var sha256 = await inspector.HashAsync(path);

        hashes.TryGet(sha256, out var hashMatch);
        var familyMatches = knowledge.MatchSignals(signals);

        if (hashMatch is not null)
        {
            await journal.RecordAsync(
                "known_bad_hash",
                "high",
                "Maverick.ThreatIntel",
                $"Known-bad hash matched: {hashMatch.Name}",
                new
                {
                    sha256,
                    name = hashMatch.Name,
                    family = hashMatch.Family,
                    source = hashMatch.Source
                },
                file: path,
                action: "block",
                result: "threat",
                risk: "high",
                evidence: new[]
                {
                    "Exact SHA-256 matched the local known-bad catalog.",
                    $"Catalog name: {hashMatch.Name}"
                });
        }

        return (hashMatch, familyMatches, sha256);
    }
}
