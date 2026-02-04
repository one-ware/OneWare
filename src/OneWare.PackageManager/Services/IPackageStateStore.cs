using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Services;

public interface IPackageStateStore
{
    Task<IReadOnlyDictionary<string, InstalledPackage>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IEnumerable<InstalledPackage> installedPackages, CancellationToken cancellationToken = default);
}
