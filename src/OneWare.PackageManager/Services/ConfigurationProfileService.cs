using System.Collections.ObjectModel;
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
    private readonly IPaths _paths;
    private readonly ILogger _logger;

    public ConfigurationProfileService(
        ISettingsService settingsService,
        IPackageService packageService,
        IPaths paths,
        ILogger<ConfigurationProfileService> logger)
    {
        _settingsService = settingsService;
        _packageService = packageService;
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

        // Export custom package sources
        ExportPackageSources(profile);

        return Task.FromResult(profile);
    }

    public async Task ImportAsync(ConfigurationProfile profile, CancellationToken cancellationToken = default)
    {
        // Apply settings
        ImportSettings(profile);

        // Add package sources first so packages can be resolved
        ImportPackageSources(profile);

        // Install packages
        await ImportPackagesAsync(profile, cancellationToken);
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

    private void ExportPackageSources(ConfigurationProfile profile)
    {
        try
        {
            if (!_settingsService.HasSetting("PackageManager_Sources")) return;

            var sources = _settingsService.GetSettingValue<ObservableCollection<string>>("PackageManager_Sources");
            foreach (var source in sources)
            {
                profile.PackageSources.Add(source);
            }
        }
        catch (Exception e)
        {
            _logger.Error("Failed to export package sources: " + e.Message, e);
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

    private void ImportPackageSources(ConfigurationProfile profile)
    {
        try
        {
            if (!_settingsService.HasSetting("PackageManager_Sources")) return;
            if (profile.PackageSources.Count == 0) return;

            var sources = _settingsService.GetSettingValue<ObservableCollection<string>>("PackageManager_Sources");
            foreach (var source in profile.PackageSources)
            {
                if (!sources.Contains(source))
                {
                    sources.Add(source);
                }
            }

            _settingsService.Save(_paths.SettingsPath, false);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to import package sources: " + e.Message, e);
        }
    }

    private async Task ImportPackagesAsync(ConfigurationProfile profile, CancellationToken cancellationToken)
    {
        if (profile.Packages.Count == 0) return;

        // Ensure package catalog is refreshed so we can resolve packages
        await _packageService.RefreshAsync();

        foreach (var packageEntry in profile.Packages)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Skip if already installed
                if (_packageService.Packages.TryGetValue(packageEntry.Id, out var existingState) &&
                    existingState.InstalledVersion != null)
                {
                    _logger.Log($"Package '{packageEntry.Id}' is already installed, skipping.");
                    continue;
                }

                PackageVersion? targetVersion = null;
                if (packageEntry.Version != null)
                {
                    targetVersion = new PackageVersion { Version = packageEntry.Version };
                }

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
        }
    }
}
