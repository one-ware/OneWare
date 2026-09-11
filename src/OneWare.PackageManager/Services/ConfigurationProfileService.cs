using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

public class ConfigurationProfileService : IConfigurationProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ISettingsService _settingsService;
    private readonly IPackageService _packageService;
    private readonly IHttpService _httpService;
    private readonly IPaths _paths;
    private readonly ILogger _logger;

    public ConfigurationProfileService(
        ISettingsService settingsService,
        IPackageService packageService,
        IHttpService httpService,
        IPaths paths,
        ILogger<ConfigurationProfileService> logger)
    {
        _settingsService = settingsService;
        _packageService = packageService;
        _httpService = httpService;
        _paths = paths;
        _logger = logger;
    }

    public Task<ConfigurationProfile> ExportAsync(CancellationToken cancellationToken = default)
    {
        var profile = new ConfigurationProfile
        {
            ExportedAt = DateTimeOffset.UtcNow
        };

        // Export settings
        ExportSettings(profile);

        // Export installed packages
        ExportPackages(profile);

        return Task.FromResult(profile);
    }

    public Task ImportAsync(ConfigurationProfile profile, CancellationToken cancellationToken = default)
    {
        return ImportAsync(profile, null, cancellationToken);
    }

    public async Task ImportAsync(ConfigurationProfile profile, IProgress<ConfigurationImportProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new ConfigurationImportProgress(ConfigurationImportStep.Settings,
            ConfigurationImportStatus.Running)
        {
            Detail = $"{profile.Settings.Count} settings"
        });
        
        // Apply settings (includes custom package sources, so packages can be resolved)
        ImportSettings(profile);

        progress?.Report(new ConfigurationImportProgress(ConfigurationImportStep.Settings,
            ConfigurationImportStatus.Completed));
        
        progress?.Report(new ConfigurationImportProgress(ConfigurationImportStep.Packages,
            ConfigurationImportStatus.Running));
        
        // Install packages
        await ImportPackagesAsync(profile, progress, cancellationToken);

        progress?.Report(new ConfigurationImportProgress(ConfigurationImportStep.Packages,
            ConfigurationImportStatus.Completed));
    }

    public async Task SaveToFileAsync(ConfigurationProfile profile, string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profile, SerializerOptions, cancellationToken);
    }

    public async Task<ConfigurationProfile> LoadFromFileAsync(string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<ConfigurationProfile>(stream, SerializerOptions,
            cancellationToken);
        return profile ?? throw new InvalidOperationException("Failed to deserialize configuration profile.");
    }

    public async Task<ConfigurationProfile> LoadFromSourceAsync(string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Profile source must not be empty.", nameof(source));

        source = source.Trim();

        if (!IsHttpUrl(source)) return await LoadFromFileAsync(source, cancellationToken);

        var content = await _httpService.DownloadTextAsync(source, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException($"Failed to download configuration profile from '{source}'.");

        return Deserialize(content);
    }

    private static bool IsHttpUrl(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static ConfigurationProfile Deserialize(string content)
    {
        var profile = JsonSerializer.Deserialize<ConfigurationProfile>(content, SerializerOptions);
        return profile ?? throw new InvalidOperationException("Failed to deserialize configuration profile.");
    }

    private void ExportSettings(ConfigurationProfile profile)
    {
        try
        {
            if (!File.Exists(_paths.SettingsPath)) return;

            using var stream = File.OpenRead(_paths.SettingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(stream, SerializerOptions);
            if (settings != null)
            {
                profile.Settings = settings;
            }
        }
        catch (Exception e)
        {
            _logger.Error("Failed to export settings: " + e.Message, e);
        }
    }

    private void ExportPackages(ConfigurationProfile profile)
    {
        try
        {
            foreach (var (id, state) in _packageService.Packages)
            {
                if (state.InstalledVersion == null) continue;

                profile.Packages.Add(new ConfigurationProfilePackage
                {
                    Id = id,
                    Version = state.InstalledVersion.Version
                });
            }
        }
        catch (Exception e)
        {
            _logger.Error("Failed to export packages: " + e.Message, e);
        }
    }

    private void ImportSettings(ConfigurationProfile profile)
    {
        try
        {
            foreach (var (key, value) in profile.Settings)
            {
                if (!_settingsService.HasSetting(key)) continue;

                try
                {
                    if (value is JsonElement je)
                    {
                        var setting = _settingsService.GetSetting(key);
                        var deserialized = je.Deserialize(setting.DefaultValue.GetType());
                        if (deserialized != null)
                        {
                            _settingsService.SetSettingValue(key, deserialized);
                        }
                    }
                    else if (value != null)
                    {
                        _settingsService.SetSettingValue(key, value);
                    }
                }
                catch (Exception e)
                {
                    _logger.Warning($"Failed to import setting '{key}': {e.Message}");
                }
            }

            _settingsService.Save(_paths.SettingsPath, false);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to import settings: " + e.Message, e);
        }
    }

    private async Task ImportPackagesAsync(ConfigurationProfile profile,
        IProgress<ConfigurationImportProgress>? progress, CancellationToken cancellationToken)
    {
        // Force a refresh so the catalog is built from the package sources of the imported profile,
        // instead of joining a refresh that was started with the previous settings
        await _packageService.RefreshAsync(true);
        
        if (profile.Packages.Count == 0) return;

        var total = profile.Packages.Count;
        var finished = 0;
        string? currentPackageId = null;
        string? currentDetail = null;

        void ReportPackages(string? detail, double? value)
        {
            progress?.Report(new ConfigurationImportProgress(ConfigurationImportStep.Packages,
                ConfigurationImportStatus.Running)
            {
                Detail = detail,
                Value = value
            });
        }

        // Feeds the download/extraction progress of the package that is currently being installed
        void OnPackageProgress(object? sender, PackageProgressEventArgs args)
        {
            if (currentPackageId == null || args.PackageId != currentPackageId) return;

            var packageProgress = args.IsIndeterminate ? 1d : Math.Clamp(args.Progress, 0f, 1f);
            ReportPackages(currentDetail, (finished + packageProgress) / total);
        }

        ReportPackages("Refreshing package sources...", 0);

        _packageService.PackageProgress += OnPackageProgress;

        try
        {
            foreach (var packageEntry in profile.Packages)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    currentDetail = $"{packageEntry.Id} ({finished + 1}/{total})";
                    ReportPackages(currentDetail, (double)finished / total);

                    if (!_packageService.Packages.TryGetValue(packageEntry.Id, out var existingState))
                    {
                        _logger.Warning($"Package '{packageEntry.Id}' was not found in any package source.");
                        continue;
                    }

                    var packageName = existingState.Package.Name ?? packageEntry.Id;
                    currentPackageId = packageEntry.Id;
                    currentDetail = $"{packageName} ({finished + 1}/{total})";
                    ReportPackages(currentDetail, (double)finished / total);

                    // Skip if already installed
                    if (existingState.InstalledVersion != null)
                    {
                        _logger.Log($"Package '{packageEntry.Id}' is already installed, skipping.");
                        continue;
                    }

                    PackageVersion? targetVersion = null;
                    if (!string.IsNullOrWhiteSpace(packageEntry.Version))
                    {
                        targetVersion =
                            existingState.Package.Versions?.FirstOrDefault(x => x.Version == packageEntry.Version);

                        if (targetVersion == null)
                        {
                            _logger.Warning(
                                $"Version '{packageEntry.Version}' of package '{packageEntry.Id}' was not found, falling back to the latest stable version.");
                        }
                    }

                    // A null target version makes the package service resolve the latest stable version
                    var result = await _packageService.InstallAsync(packageEntry.Id, targetVersion, false, false,
                        cancellationToken);

                    if (result.Status == PackageInstallResultReason.Installed)
                    {
                        _logger.Log($"Successfully installed package '{packageEntry.Id}'.");
                    }
                    else
                    {
                        _logger.Warning(
                            $"Failed to install package '{packageEntry.Id}': {result.Status}");
                    }
                }
                catch (Exception e)
                {
                    _logger.Warning($"Error installing package '{packageEntry.Id}': {e.Message}");
                }
                finally
                {
                    currentPackageId = null;
                    finished++;
                    ReportPackages(currentDetail, (double)finished / total);
                }
            }
        }
        finally
        {
            _packageService.PackageProgress -= OnPackageProgress;
        }
    }
}
