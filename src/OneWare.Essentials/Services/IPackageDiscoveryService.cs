namespace OneWare.Essentials.Services;

public interface IPackageDiscoveryService
{
    IReadOnlyList<string> FeaturedPackageIds { get; }
    void RegisterOfficialSource(string url);
}