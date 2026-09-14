using Microsoft.Win32;

namespace Maverick.Core;

public sealed class ServiceConfigurationMonitor
{
    private const string RegistryPath = @"SYSTEM\CurrentControlSet\Services\Maverick Core";
    private const string ExpectedAccount = @"NT AUTHORITY\LocalService";

    private readonly Journal journal;

    public ServiceConfigurationMonitor(Journal journal)
    {
        this.journal = journal;
    }

    public async Task<bool> VerifyAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
            if (key is null)
            {
                await RecordAsync("Maverick Core service registry configuration is missing.", "missing");
                return false;
            }

            var imagePath = Normalize(key.GetValue("ImagePath")?.ToString());
            var expectedExe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Maverick", "Core", "Maverick.Core.exe");

            var start = Convert.ToInt32(key.GetValue("Start", -1));
            var account = key.GetValue("ObjectName")?.ToString();
            var type = Convert.ToInt32(key.GetValue("Type", -1));

            var problems = new List<string>();

            if (!string.Equals(imagePath, expectedExe, StringComparison.OrdinalIgnoreCase))
                problems.Add("Unexpected service executable path.");

            if (start != 2)
                problems.Add("Service is not configured for automatic startup.");

            if (!string.Equals(account, ExpectedAccount, StringComparison.OrdinalIgnoreCase))
                problems.Add("Service account is not LocalService.");

            if (type != 0x10)
                problems.Add("Service type is not a Win32 own-process service.");

            if (problems.Count == 0)
                return true;

            await journal.RecordAsync(
                "service_tamper",
                "high",
                "Maverick.ServiceSecurity",
                "Maverick Core service configuration differs from its expected configuration.",
                new { imagePath, expectedExe, start, account, type, problems },
                action: "verify-service-configuration",
                result: "changed",
                risk: "high",
                evidence: problems);

            return false;
        }
        catch (Exception ex)
        {
            await RecordAsync(
                "Maverick Core service configuration could not be verified.",
                "error",
                ex.Message);

            return false;
        }
    }

    private Task RecordAsync(string summary, string result, string? error = null) =>
        journal.RecordAsync(
            "service_tamper",
            "high",
            "Maverick.ServiceSecurity",
            summary,
            new { error },
            action: "verify-service-configuration",
            result: result,
            risk: "high");

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }
}
