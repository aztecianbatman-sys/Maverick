namespace Maverick.Core;

public sealed class ScanService
{
    private readonly FileInspector files;
    private readonly Journal journal;
    private readonly ProtectionService protection;

    public ScanService(FileInspector files, Journal journal, ProtectionService protection)
    {
        this.files = files;
        this.journal = journal;
        this.protection = protection;
    }

    public async Task<object> ScanAsync(string path, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(path);

        if (File.Exists(root))
        {
            var analysis = await protection.AnalyzeFileAsync(
                root,
                allowQuarantine: true,
                cancellationToken);

            await journal.RecordAsync(
                "scan",
                analysis is not null ? "info" : "warning",
                "Maverick.Core",
                $"File scan completed for {root}",
                analysis,
                file: root,
                action: "scan",
                result: "completed",
                risk: "info");

            return analysis;
        }

        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);

        long inspected = 0;
        long failed = 0;
        long threats = 0;
        long suspicious = 0;
        var started = DateTimeOffset.UtcNow;

        foreach (var file in EnumerateFilesSafe(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await protection.AnalyzeFileAsync(
                    file,
                    allowQuarantine: true,
                    cancellationToken);

                inspected++;

                if (result is not null &&
                    result.GetType().GetProperty("verdict")?.GetValue(result) is ProtectionVerdict verdict)
                {
                    if (verdict.Verdict == "Threat") threats++;
                    if (verdict.Verdict == "Suspicious") suspicious++;
                }
            }
            catch
            {
                failed++;
            }
        }

        var completed = DateTimeOffset.UtcNow;

        await journal.RecordAsync(
            "scan",
            "info",
            "Maverick.Core",
            $"Inventory scan completed for {root}",
            new
            {
                root,
                inspected,
                failed,
                threats,
                suspicious,
                startedUtc = started,
                completedUtc = completed
            });

        return new
        {
            root,
            inspected,
            failed,
            threats,
            suspicious,
            startedUtc = started,
            completedUtc = completed,
            verdict = "protection-analysis"
        };
    }

    private async Task<object> InspectOneAsync(
        string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await files.InspectAsync(path, true);

        await journal.RecordAsync(
            type: "file_inspection",
            severity: "info",
            source: "Maverick.Core",
            summary: $"Inspected {Path.GetFileName(path)}",
            details: result,
            file: path,
            action: "inspect",
            result: "observed",
            risk: "unknown");

        return new
        {
            result,
            verdict = "inventory-only"
        };
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };

        return Directory.EnumerateFiles(root, "*", options);
    }
}
