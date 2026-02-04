using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

public class PackageStateStore : IPackageStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private readonly ILogger _logger;
    private readonly string _databasePath;

    public PackageStateStore(IPaths paths, ILogger logger)
    {
        _logger = logger;
        _databasePath = Path.Combine(paths.PackagesDirectory,
            $"{paths.AppName.ToLower().Replace(" ", "")}-packages.json");
    }

    public async Task<IReadOnlyDictionary<string, InstalledPackage>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var installed = new Dictionary<string, InstalledPackage>();

        try
        {
            if (!File.Exists(_databasePath)) return installed;

            await using var file = File.OpenRead(_databasePath);
            var installedPackages = await JsonSerializer.DeserializeAsync<InstalledPackage[]>(file, SerializerOptions,
                cancellationToken);

            if (installedPackages == null) return installed;

            foreach (var package in installedPackages)
                installed[package.Id] = package;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return installed;
    }

    public async Task SaveAsync(IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

            await using var file = File.OpenWrite(_databasePath);
            file.SetLength(0);

            await JsonSerializer.SerializeAsync(file, installedPackages, SerializerOptions, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}
