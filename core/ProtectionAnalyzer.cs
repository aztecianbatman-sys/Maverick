using System.Text;

namespace Maverick.Core;

public sealed record ProtectionVerdict(
    string Verdict,
    string Risk,
    int Score,
    IReadOnlyList<string> Evidence,
    string Action);

public sealed class ProtectionAnalyzer
{
    private static readonly HashSet<string> ScriptLikeExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".ps1", ".psm1", ".bat", ".cmd", ".vbs", ".vbe",
            ".js", ".jse", ".wsf", ".wsh", ".hta"
        };

    private static readonly HashSet<string> ExecutableExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".scr", ".com", ".msi"
        };

    private readonly ThreatIntelService threatIntel;
    private readonly AmsiScanner amsi;

    public ProtectionAnalyzer(
        ThreatIntelService threatIntel,
        AmsiScanner amsi)
    {
        this.threatIntel = threatIntel;
        this.amsi = amsi;
    }

    public async Task<ProtectionVerdict> AnalyzeFileAsync(
        string path,
        IEnumerable<string>? observedSignals = null)
    {
        if (!File.Exists(path))
        {
            return new ProtectionVerdict(
                "Unknown", "Unknown", 0,
                new[] { "File disappeared before analysis." },
                "Ignore");
        }

        var fullPath = Path.GetFullPath(path);
        var extension = Path.GetExtension(fullPath);
        var evidence = new List<string>();
        var score = 0;

        var signals = new HashSet<string>(
            observedSignals ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        if (ExecutableExtensions.Contains(extension))
        {
            evidence.Add("Executable file type.");
            score += 5;
        }

        if (ScriptLikeExtensions.Contains(extension))
        {
            evidence.Add("Script-capable file type.");
            score += 10;
        }

        var lower = fullPath.ToLowerInvariant();

        if (lower.Contains("\\downloads\\"))
        {
            evidence.Add("File is under a Downloads directory.");
            signals.Add("downloaded-file");
            score += 5;
        }

        if (lower.Contains("\\appdata\\local\\temp\\") ||
            lower.Contains("\\windows\\temp\\"))
        {
            evidence.Add("File is under a temporary directory.");
            signals.Add("temporary-location");
            score += 10;
        }

        if (extension is ".ps1" or ".psm1")
            signals.Add("powershell");

        if (extension is ".bat" or ".cmd")
            signals.Add("windows-command-shell");

        if (extension is ".vbs" or ".vbe" or ".wsf" or ".wsh" or ".hta")
            signals.Add("visual-basic-script");

        try
        {
            var amsiDetected = await amsi.ScanFileAsync(
                fullPath,
                CancellationToken.None);

            if (amsiDetected == true)
            {
                evidence.Add("AMSI reported malicious script content.");

                return new ProtectionVerdict(
                    "Threat",
                    "High",
                    100,
                    evidence,
                    "Quarantine");
            }

            if (amsiDetected == false)
                evidence.Add("AMSI did not report malicious script content.");
        }
        catch
        {
            // AMSI is an enrichment source; failure is never treated as safe.
        }

        var intel = await threatIntel.ComputeAndLookupAsync(
            fullPath,
            signals,
            CancellationToken.None);

        if (intel.HashMatch is not null)
        {
            evidence.Add("Exact SHA-256 matched the local known-bad catalog.");
            evidence.Add($"Known-bad entry: {intel.HashMatch.Name}");

            return new ProtectionVerdict(
                "Threat",
                "High",
                100,
                evidence,
                intel.HashMatch.Action.Equals(
                    "quarantine",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Quarantine"
                    : "Alert");
        }

        foreach (var family in intel.FamilyMatches)
            evidence.Add(
                $"Behavior affinity: {family} (not family attribution).");

        if (intel.FamilyMatches.Count > 0)
            score += Math.Min(
                20,
                intel.FamilyMatches.Count * 8);

        var risk = score >= 25 ? "Medium" : "Low";
        var verdict = score >= 25 ? "Suspicious" : "Safe";

        return new ProtectionVerdict(
            verdict,
            risk,
            score,
            evidence,
            "Alert");
    }
}
