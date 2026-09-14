namespace Maverick.Core;

public sealed class CorePaths
{
    public string RootDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Maverick");

    public string DatabasePath => Path.Combine(RootDirectory, "maverick.db");
    public string QuarantineDirectory => Path.Combine(RootDirectory, "Quarantine");
    public string ConfigPath => Path.Combine(RootDirectory, "config.json");
    public string IntegrityManifestPath => Path.Combine(RootDirectory, "integrity.json");

    public CorePaths()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(QuarantineDirectory);
    }
}
