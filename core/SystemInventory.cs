using Microsoft.Win32;
using System.Diagnostics;
using System.ServiceProcess;

namespace Maverick.Core;

public sealed class SystemInventory
{
    public object ListProcesses()
    {
        var results = new List<object>();

        foreach (var process in Process.GetProcesses().OrderBy(p => p.ProcessName))
        {
            try
            {
                string? path = null;
                try { path = process.MainModule?.FileName; } catch { }

                results.Add(new
                {
                    pid = process.Id,
                    name = process.ProcessName,
                    path,
                    sessionId = process.SessionId
                });
            }
            catch
            {
                // A process may exit between enumeration and inspection.
            }
            finally
            {
                process.Dispose();
            }
        }

        return results;
    }

    public object ListServices()
    {
        return ServiceController.GetServices()
            .OrderBy(s => s.ServiceName)
            .Select(s => new
            {
                name = s.ServiceName,
                displayName = s.DisplayName,
                status = s.Status.ToString(),
                serviceType = s.ServiceType.ToString()
            })
            .ToList();
    }

    public async Task<object> ListScheduledTasks()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Query /FO CSV /V",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start schtasks.exe.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(stderr.Trim());

        return new { rawCsv = stdout };
    }

    public object ListStartupLocations()
    {
        var rows = new List<object>();

        AddRunKey(rows, Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM");

        AddRunKey(rows, Registry.LocalMachine,
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM-WOW6432");

        using var users = Registry.Users;

        foreach (var sid in users.GetSubKeyNames().Where(IsUserSid))
        {
            using var key = users.OpenSubKey(
                $@"{sid}\Software\Microsoft\Windows\CurrentVersion\Run");

            if (key is null) continue;

            foreach (var name in key.GetValueNames())
            {
                rows.Add(new
                {
                    location = $@"HKEY_USERS\{sid}",
                    name,
                    value = key.GetValue(name)?.ToString() ?? ""
                });
            }
        }

        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        if (Directory.Exists(common))
        {
            rows.AddRange(Directory.EnumerateFileSystemEntries(common).Select(path => new
            {
                location = "Common Startup folder",
                name = Path.GetFileName(path),
                value = path
            }));
        }

        return rows;
    }

    private static bool IsUserSid(string sid) =>
        sid.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase);

    private static void AddRunKey(List<object> rows, RegistryKey hive, string path, string label)
    {
        using var key = hive.OpenSubKey(path);
        if (key is null) return;

        foreach (var name in key.GetValueNames())
        {
            rows.Add(new
            {
                location = label,
                name,
                value = key.GetValue(name)?.ToString() ?? ""
            });
        }
    }
}
