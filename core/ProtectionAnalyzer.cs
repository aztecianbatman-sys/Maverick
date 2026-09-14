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

    private const string EicarMarker =
        @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    private readonly ThreatIntelService threatIntel;

    public ProtectionAnalyzer(ThreatIntelService threatIntel)
    {
        this.threatIntel = threatIntel;
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

        if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
            signals.Add("powershell");

        if (extension is ".bat" or ".cmd")
            signals.Add("windows-command-shell");

        if (extension is ".vbs" or ".vbe")
            signals.Add("visual-basic-script");

        var sha256 = await threatIntel.ComputeAndLookupAsync(
            fullPath,
            signals,
            CancellationToken.None);

        if (sha256.HashMatch is not null)
        {
            evidence.Add("Exact SHA-256 matched the local known-bad catalog.");
            evidence.Add($"Known-bad entry: {sha256.HashMatch.Name}");

            return new ProtectionVerdict(
                "Threat",
                "High",
                100,
                evidence,
                sha256.HashMatch.Action.Equals(
                    "quarantine",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Quarantine"
                    : "Alert");
        }

        foreach (var family in sha256.FamilyMatches)
            evidence.Add($"Behavior affinity: {family} (not family attribution).");

        if (sha256.FamilyMatches.Count > 0)
            score += Math.Min(20, sha256.FamilyMatches.Count * 8);

        var risk = score >= 25 ? "Medium" : "Low";
        var verdict = score >= 25 ? "Suspicious" : "Safe";

        return new ProtectionVerdict(
            verdict,
            risk,
            score,
            evidence,
            "Alert");
    }

    internal static bool IsEicarText(string text) =>
        string.Equals(text.TrimEnd('\r', '\n'), EicarMarker, StringComparison.Ordinal);
}
