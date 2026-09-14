namespace OneWare.Essentials.PackageManager;

public class PackageRepository
{
    public PackageManifest[]? Packages { get; init; }
    public string[]? FeaturedPackageIds { get; init; }
}