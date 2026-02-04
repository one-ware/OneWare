using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Services;

public interface IPackageCatalog
{
    IReadOnlyDictionary<string, Package> Manifests { get; }

    Task<bool> RefreshAsync(IEnumerable<string> sources, CancellationToken cancellationToken = default);

    void RegisterStandalone(Package package);
}
