using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Services;

public interface IPackageCatalog
{
    IReadOnlyDictionary<string, Package> Manifests { get; }

    Task<bool> RefreshAsync(IEnumerable<string[]> repositories, CancellationToken cancellationToken = default);

    void RegisterStandalone(Package package);
}
