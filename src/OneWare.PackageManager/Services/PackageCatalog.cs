using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using Microsoft.Extensions.Logging;

namespace OneWare.PackageManager.Services;

public class PackageCatalog : IPackageCatalog, IPackageDiscoveryService
{
    private readonly HashSet<string> _officialSources = new(StringComparer.Ordinal);
    public IReadOnlyList<string> FeaturedPackageIds { get; private set; } = [];
    public void RegisterOfficialSource(string url) => _officialSources.Add(url);
    private readonly IPackageRepositoryClient _repositoryClient;
    private readonly ILogger _logger;
    private readonly List<Package> _standalonePackages = [];
    private readonly Dictionary<string, Package> _manifests = new();

    public PackageCatalog(IPackageRepositoryClient repositoryClient, ILogger logger)
    {
        _repositoryClient = repositoryClient;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, Package> Manifests => _manifests;

    public void RegisterStandalone(Package package)
    {
        _standalonePackages.Add(package);
        if (package.Id != null)
        {
            _manifests[package.Id] = package;
            FeaturedPackageIds = FeaturedPackageIds.Where(x => x != package.Id).ToArray();
        }
    }

    public async Task<bool> RefreshAsync(IEnumerable<string[]> repositories, CancellationToken cancellationToken = default)
    {
        var result = true;
        var newPackages = new Dictionary<string, Package>();
        var promoted = new Dictionary<string, bool>(StringComparer.Ordinal);
        var featuredOrder = new List<string>();

        foreach (var repository in repositories)
        {
            IReadOnlyList<Package> loaded = new List<Package>();
            string? winningSource = null;
            foreach (var source in repository)
            {
                try
                {
                    loaded = await _repositoryClient.LoadRepositoryAsync(source, cancellationToken);
                    if(loaded.Count == 0)
                    {
                        continue;
                    }
                    winningSource = source;
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    _logger.Error($"Failed to refresh package source '{source}'.", e);
                    result = false;
                    continue;
                }
            }

            if (loaded.Count == 0)
            {
                result = false;
                continue;
            }

            foreach (var package in loaded)
            {
                if (package.Id == null) continue;
                newPackages[package.Id] = package;
                var curated = winningSource != null && _officialSources.Contains(winningSource)
                    ? new[] { "OneWare.AI" }.Concat((_repositoryClient as PackageRepositoryClient)?.GetFeaturedIds(winningSource) ?? []).Distinct().ToArray()
                    : [];
                promoted[package.Id] = curated.Contains(package.Id);
                foreach (var id in curated) if (!featuredOrder.Contains(id)) featuredOrder.Add(id);
            }
        }

        foreach (var package in _standalonePackages)
        {
            if (package.Id != null) { newPackages[package.Id] = package; promoted[package.Id] = false; }
        }

        _manifests.Clear();
        foreach (var (id, pkg) in newPackages)
            _manifests[id] = pkg;
        FeaturedPackageIds = featuredOrder.Where(id => promoted.GetValueOrDefault(id) && newPackages.ContainsKey(id)).ToArray();

        return result;
    }
}
