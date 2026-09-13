namespace Maverick.Core;

public sealed class ProtectionService
{
    private readonly ProtectionAnalyzer analyzer;
    private readonly QuarantineService quarantine;
    private readonly Journal journal;
    private readonly DownloadOriginReader origins;

    public ProtectionService(
        ProtectionAnalyzer analyzer,
        QuarantineService quarantine,
        Journal journal,
        DownloadOriginReader origins)
    {
        this.analyzer = analyzer;
        this.quarantine = quarantine;
        this.journal = journal;
        this.origins = origins;
    }

    public async Task<object> AnalyzeFileAsync(
        string path,
        bool allowQuarantine,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        var verdict = await analyzer.AnalyzeFileAsync(fullPath);
        var origin = await origins.ReadAsync(fullPath);

        var evidence = verdict.Evidence.ToList();

        if (origin is not null)
        {
            evidence.Add("Windows Zone.Identifier metadata present.");
            if (!string.IsNullOrWhiteSpace(origin.HostUrl))
                evidence.Add("Download host metadata present.");
            if (!string.IsNullOrWhiteSpace(origin.ReferrerUrl))
                evidence.Add("Download referrer metadata present.");
        }

        await journal.RecordAsync(
            type: "protection_verdict",
            severity: verdict.Risk is "High" ? "high" : verdict.Risk is "Medium" ? "warning" : "info",
            source: "Maverick.Protection",
            summary: $"{verdict.Verdict}: {Path.GetFileName(fullPath)}",
            details: new { verdict, downloadOrigin = origin },
            file: fullPath,
            action: verdict.Action,
            result: verdict.Verdict,
            risk: verdict.Risk,
            evidence: evidence);

        if (allowQuarantine &&
            verdict.Verdict == "Threat" &&
            verdict.Action == "Quarantine")
        {
            return new
            {
                verdict,
                downloadOrigin = origin,
                containment = await quarantine.QuarantineAsync(
                    fullPath,
                    string.Join(" ", evidence),
                    evidence)
            };
        }

        return new
        {
            verdict,
            downloadOrigin = origin,
            containment = (object?)null
        };
    }
}
