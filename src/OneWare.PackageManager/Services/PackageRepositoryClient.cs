using System.Text.Json;
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
        try
        {
            var repositoryString = await _httpService.DownloadTextAsync(url, TimeSpan.FromSeconds(10), cancellationToken);
            if (repositoryString == null) return Array.Empty<Package>();

            var packages = new List<Package>();

            if (IsRepositoryJson(repositoryString))
            {
                try
                {
                    var repository = JsonSerializer.Deserialize<PackageRepository>(repositoryString, SerializerOptions);

                    if (repository is { Packages: not null })
                        foreach (var manifest in repository.Packages)
                            try
                            {
                                var package = await LoadPackageManifestAsync(manifest, cancellationToken);

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
                catch (JsonException e)
                {
                    _logger.Error($"Invalid package source format at '{url}'. Expected repository JSON with a 'packages' array.", e);
                    return Array.Empty<Package>();
                }
                catch (Exception e)
                {
                    _logger.Error(e.Message, e);
                    return Array.Empty<Package>();
                }
            }
            else
            {
                try
                {
                    var package = JsonSerializer.Deserialize<Package>(repositoryString, SerializerOptions);
                    if (package != null) packages.Add(package);
                }
                catch (JsonException e)
                {
                    _logger.Error($"Invalid package source format at '{url}'. Expected a package manifest JSON object.", e);
                    return Array.Empty<Package>();
                }
            }

            return packages;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.Error($"Failed to load package source '{url}'.", e);
            return Array.Empty<Package>();
        }
    }

    private async Task<Package?> LoadPackageManifestAsync(PackageManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestContent = GetManifestContent(manifest.Content);

        if (manifestContent == null)
        {
            if (string.IsNullOrWhiteSpace(manifest.ManifestUrl))
                return null;

            manifestContent = await _httpService.DownloadTextAsync(manifest.ManifestUrl, cancellationToken: cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(manifestContent))
            return null;

        var package = JsonSerializer.Deserialize<Package>(manifestContent, SerializerOptions);

        if (package != null && string.IsNullOrWhiteSpace(package.Icon) && !string.IsNullOrWhiteSpace(manifest.Icon))
            package.Icon = manifest.Icon;

        return package;
    }

    private static bool IsRepositoryJson(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true
        });

        return document.RootElement.ValueKind == JsonValueKind.Object
               && document.RootElement.TryGetProperty("packages", out var packages)
               && packages.ValueKind == JsonValueKind.Array;
    }

    private static string? GetManifestContent(JsonElement? content)
    {
        if (content is not { } contentElement) return null;

        return contentElement.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(contentElement.GetString())
                ? null
                : contentElement.GetString(),
            JsonValueKind.Object => contentElement.GetRawText(),
            _ => null
        };
    }
}
