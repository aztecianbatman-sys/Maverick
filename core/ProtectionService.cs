namespace Maverick.Core;

public sealed class ProtectionService
{
    private readonly ProtectionAnalyzer analyzer;
    private readonly QuarantineService quarantine;
    private readonly Journal journal;

    public ProtectionService(
        ProtectionAnalyzer analyzer,
        QuarantineService quarantine,
        Journal journal)
    {
        this.analyzer = analyzer;
        this.quarantine = quarantine;
        this.journal = journal;
    }

    public async Task<object> AnalyzeFileAsync(
        string path,
        bool allowQuarantine,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        var verdict = await analyzer.AnalyzeFileAsync(fullPath);

        await journal.RecordAsync(
            type: "protection_verdict",
            severity: verdict.Risk is "High" ? "high" : verdict.Risk is "Medium" ? "warning" : "info",
            source: "Maverick.Protection",
            summary: $"{verdict.Verdict}: {Path.GetFileName(fullPath)}",
            details: verdict,
            file: fullPath,
            action: verdict.Action,
            result: verdict.Verdict,
            risk: verdict.Risk,
            evidence: verdict.Evidence);

        if (allowQuarantine &&
            verdict.Verdict == "Threat" &&
            verdict.Action == "Quarantine")
        {
            return new
            {
                verdict,
                containment = await quarantine.QuarantineAsync(
                    fullPath,
                    string.Join(" ", verdict.Evidence),
                    verdict.Evidence)
            };
        }

        return new
        {
            verdict,
            containment = (object?)null
        };
    }
}
