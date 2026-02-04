using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

public partial class PackageRepositoryClient : IPackageRepositoryClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private readonly IHttpService _httpService;
    private readonly ILogger _logger;

    public PackageRepositoryClient(IHttpService httpService, ILogger logger)
    {
        _httpService = httpService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Package>> LoadRepositoryAsync(string url,
        CancellationToken cancellationToken = default)
    {
        var repositoryString = await _httpService.DownloadTextAsync(url, TimeSpan.FromSeconds(10));
        if (repositoryString == null) return Array.Empty<Package>();

        var trimmed = WhitespaceRegex().Replace(repositoryString, "");
        var packages = new List<Package>();

        if (trimmed.StartsWith("{\"packages\":", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var repository = JsonSerializer.Deserialize<PackageRepository>(repositoryString, SerializerOptions);

                if (repository is { Packages: not null })
                    foreach (var manifest in repository.Packages)
                        try
                        {
                            if (manifest.ManifestUrl == null) continue;

                            var downloadManifest =
                                await _httpService.DownloadTextAsync(manifest.ManifestUrl);

                            var package = JsonSerializer.Deserialize<Package>(downloadManifest!, SerializerOptions);

                            if (package != null) packages.Add(package);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e.Message, e);
                        }
                else
                {
                    throw new Exception("Packages empty");
                }
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }
        }
        else
        {
            var package = JsonSerializer.Deserialize<Package>(repositoryString, SerializerOptions);
            if (package != null) packages.Add(package);
        }

        return packages;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
