using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

public class PackageCatalog : IPackageCatalog
{
    private readonly IPackageRepositoryClient _repositoryClient;
    private readonly List<Package> _standalonePackages = [];
    private readonly Dictionary<string, Package> _manifests = new();

    public PackageCatalog(IPackageRepositoryClient repositoryClient)
    {
        _repositoryClient = repositoryClient;
    }

    public IReadOnlyDictionary<string, Package> Manifests => _manifests;

    public void RegisterStandalone(Package package)
    {
        _standalonePackages.Add(package);
        if (package.Id != null)
            _manifests[package.Id] = package;
    }

    public async Task<bool> RefreshAsync(IEnumerable<string> sources, CancellationToken cancellationToken = default)
    {
        var result = true;
        var newPackages = new Dictionary<string, Package>();

        foreach (var source in sources)
        {
            var loaded = await _repositoryClient.LoadRepositoryAsync(source, cancellationToken);
            if (loaded.Count == 0)
            {
                result = false;
                continue;
            }

            foreach (var package in loaded)
            {
                if (package.Id == null) continue;
                newPackages[package.Id] = package;
            }
        }

        foreach (var package in _standalonePackages)
        {
            if (package.Id != null) newPackages[package.Id] = package;
        }

        _manifests.Clear();
        foreach (var (id, pkg) in newPackages)
            _manifests[id] = pkg;

        return result;
    }
}
