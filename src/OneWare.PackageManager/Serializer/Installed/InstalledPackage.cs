namespace OneWare.PackageManager.Serializer.Installed;

public class InstalledPackage
{
    public Package Package { get; }
    public string Version { get; }

    public InstalledPackage(Package package, string version)
    {
        Package = package;
        Version = version;
    }
}