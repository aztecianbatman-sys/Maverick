using System.Runtime.InteropServices;

namespace Maverick.Core;

public sealed class AmsiScanner
{
    [StructLayout(LayoutKind.Sequential)]
    private struct AmsiResult { public int Value; }

    [DllImport("amsi.dll", CharSet = CharSet.Unicode)]
    private static extern int AmsiInitialize(string appName, out IntPtr context);

    [DllImport("amsi.dll")]
    private static extern void AmsiUninitialize(IntPtr context);

    [DllImport("amsi.dll")]
    private static extern int AmsiOpenSession(IntPtr context, out IntPtr session);

    [DllImport("amsi.dll")]
    private static extern void AmsiCloseSession(IntPtr context, IntPtr session);

    [DllImport("amsi.dll", CharSet = CharSet.Unicode)]
    private static extern int AmsiScanBuffer(
        IntPtr context, byte[] buffer, uint length, string contentName,
        IntPtr session, out AmsiResult result);

    private const int AmsiResultDetected = 32768;

    public async Task<bool?> ScanFileAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(path);
        if (extension is not (".ps1" or ".psm1" or ".js" or ".jse" or
            ".vbs" or ".vbe" or ".wsf" or ".wsh" or ".hta" or ".bat" or ".cmd"))
            return null;

        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(path, token);
        }
        catch
        {
            return null;
        }

        if (content.Length == 0) return null;

        const int maxBytes = 8 * 1024 * 1024;
        if (content.Length > maxBytes) content = content[..maxBytes];

        var hr = AmsiInitialize("Maverick.Core", out var context);
        if (hr < 0) return null;

        try
        {
            hr = AmsiOpenSession(context, out var session);
            if (hr < 0) return null;

            try
            {
                hr = AmsiScanBuffer(
                    context, content, (uint)content.Length, path, session, out var result);

                return hr < 0 ? null : result.Value >= AmsiResultDetected;
            }
            finally
            {
                AmsiCloseSession(context, session);
            }
        }
        finally
        {
            AmsiUninitialize(context);
        }
    }
}
