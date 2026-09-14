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
                cancellationToken).ConfigureAwait(false);

            if (installedPackages == null) return installed;

            foreach (var package in installedPackages)
                installed[package.Id] = package;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            // A corrupt database is not an empty installation. In particular, startup must not
            // bypass dependency ordering by treating every managed directory as a legacy plugin.
            throw;
        }

        return installed;
    }

    public async Task SaveAsync(IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default)
    {
        var temporaryPath = _databasePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(file, installedPackages, SerializerOptions, cancellationToken);
                await file.FlushAsync(cancellationToken);
                file.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _databasePath, true);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
