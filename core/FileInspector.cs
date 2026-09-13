using System.Security.Cryptography;

namespace Maverick.Core;

public sealed class FileInspector
{
    public async Task<object> InspectAsync(string path, bool includeHash = true)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A file path is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
            throw new FileNotFoundException("File not found.", fullPath);

        string? hash = null;
        if (includeHash)
            hash = await HashAsync(fullPath);

        return new
        {
            path = fullPath,
            name = info.Name,
            extension = info.Extension,
            sizeBytes = info.Length,
            createdUtc = info.CreationTimeUtc,
            modifiedUtc = info.LastWriteTimeUtc,
            sha256 = hash
        };
    }

    public async Task<string> HashAsync(string path)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 1024, useAsync: true);

        using var sha = SHA256.Create();
        var digest = await sha.ComputeHashAsync(stream);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
