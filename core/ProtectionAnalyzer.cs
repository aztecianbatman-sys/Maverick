using System.Security.Cryptography;
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
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    public async Task<ProtectionVerdict> AnalyzeFileAsync(string path)
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

        var eicar = await IsEicarTestFileAsync(fullPath);
        if (eicar)
        {
            return new ProtectionVerdict(
                "Threat",
                "High",
                100,
                new[] { "EICAR antivirus test signature matched." },
                "Quarantine");
        }

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
            score += 5;
        }

        if (lower.Contains("\\appdata\\local\\temp\\") ||
            lower.Contains("\\windows\\temp\\"))
        {
            evidence.Add("File is under a temporary directory.");
            score += 10;
        }

        var risk = score >= 20 ? "Medium" : "Low";
        var verdict = score >= 20 ? "Suspicious" : "Safe";

        return new ProtectionVerdict(
            verdict,
            risk,
            score,
            evidence,
            "Alert");
    }

    private static async Task<bool> IsEicarTestFileAsync(string path)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var text = Encoding.ASCII.GetString(bytes).TrimEnd('\r', '\n');
            return string.Equals(text, EicarMarker, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
