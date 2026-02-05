using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Services;

public interface IPackageRepositoryClient
{
    Task<IReadOnlyList<Package>> LoadRepositoryAsync(string url, CancellationToken cancellationToken = default);
}
