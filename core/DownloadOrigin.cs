using System.Text;

namespace Maverick.Core;

public sealed record DownloadOrigin(string? ZoneId, string? HostUrl, string? ReferrerUrl);

public sealed class DownloadOriginReader
{
    public async Task<DownloadOrigin?> ReadAsync(string path)
    {
        var zonePath = path + ":Zone.Identifier";
        if (!File.Exists(zonePath))
            return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(zonePath, Encoding.Unicode);
            string? zone = null;
            string? host = null;
            string? referrer = null;

            foreach (var line in lines)
            {
                var separator = line.IndexOf('=');
                if (separator <= 0) continue;

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();

                if (key.Equals("ZoneId", StringComparison.OrdinalIgnoreCase)) zone = value;
                if (key.Equals("HostUrl", StringComparison.OrdinalIgnoreCase)) host = value;
                if (key.Equals("ReferrerUrl", StringComparison.OrdinalIgnoreCase)) referrer = value;
            }

            return new DownloadOrigin(zone, host, referrer);
        }
        catch
        {
            return null;
        }
    }
}
