namespace Maverick.Core;

public sealed record ThreatProfile(
    string Name,
    string Family,
    string Category,
    string[] Techniques,
    string[] Signals);

public sealed class ThreatKnowledgeCatalog
{
    public IReadOnlyList<ThreatProfile> Profiles { get; } = new[]
    {
        new ThreatProfile(
            "WannaCry", "S0366", "Ransomware/Worm",
            new[] { "T1486", "T1059", "T1135", "T1210" },
            new[] { "destructive-burst", "network-share-discovery", "smb-propagation" }),

        new ThreatProfile(
            "NotPetya", "S0368", "Wiper/Worm",
            new[] { "T1486", "T1490", "T1210", "T1135" },
            new[] { "destructive-burst", "recovery-inhibition", "network-share-discovery" }),

        new ThreatProfile(
            "Emotet", "S0367", "Modular Trojan",
            new[] { "T1547.001", "T1059.001", "T1053.005", "T1543.003", "T1055", "T1105" },
            new[] { "run-key-persistence", "powershell", "scheduled-task", "service-persistence", "process-injection", "download-follow-on" }),

        new ThreatProfile(
            "TrickBot", "S0266", "Trojan",
            new[] { "T1057", "T1055", "T1071.001", "T1027", "T1566" },
            new[] { "process-discovery", "process-injection", "web-c2", "obfuscation", "phishing" }),

        new ThreatProfile(
            "LockBit 2.0", "S1199", "Ransomware",
            new[] { "T1547.001", "T1059.001", "T1053.005", "T1135", "T1486", "T1489" },
            new[] { "run-key-persistence", "powershell", "scheduled-task", "network-share-discovery", "destructive-burst", "service-stop" }),

        new ThreatProfile(
            "LockBit 3.0", "S1202", "Ransomware",
            new[] { "T1547.004", "T1071.001", "T1548.002", "T1486" },
            new[] { "winlogon-persistence", "web-c2", "uac-abuse", "destructive-burst" }),

        new ThreatProfile(
            "JCry", "S0389", "Ransomware",
            new[] { "T1547.001", "T1059.001", "T1486", "T1490" },
            new[] { "run-key-persistence", "powershell", "destructive-burst", "recovery-inhibition" }),

        new ThreatProfile(
            "Conti", "S0575", "Ransomware",
            new[] { "T1059.003", "T1486" },
            new[] { "windows-command-shell", "destructive-burst" }),

        new ThreatProfile(
            "Ragnar Locker", "S0481", "Ransomware",
            new[] { "T1059.003", "T1543.003", "T1486" },
            new[] { "windows-command-shell", "service-persistence", "destructive-burst" }),

        new ThreatProfile(
            "LokiBot", "S0447", "Information Stealer",
            new[] { "T1555", "T1056", "T1082" },
            new[] { "credential-access", "browser-credential-theft", "system-discovery" })
    };

    public IReadOnlyList<string> MatchSignals(IEnumerable<string> signals)
    {
        var set = signals.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Profiles
            .Select(profile => new
            {
                profile.Name,
                score = profile.Signals.Count(set.Contains)
            })
            .Where(x => x.score >= 2)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.Name)
            .Take(3)
            .Select(x => x.Name)
            .ToList();
    }
}
