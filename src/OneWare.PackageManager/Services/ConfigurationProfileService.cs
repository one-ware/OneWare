using System.Collections.ObjectModel;
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
    private const string AppliedMarkerFileName = "configuration-profile.applied";

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

    public async Task<bool> ApplyEnvironmentProfileAsync(CancellationToken cancellationToken = default)
    {
        var source = Environment.GetEnvironmentVariable(IConfigurationProfileService.ProfileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(source)) return false;

        source = source.Trim();

        try
        {
            var profile = await LoadFromSourceAsync(source, cancellationToken);

            // "once" (default) applies a given profile only when its content changed since the last
            // run, so a deployment default does not overwrite the user's own settings on every
            // launch. "always" re-applies unconditionally for locked-down deployments.
            var alwaysApply = string.Equals(
                Environment.GetEnvironmentVariable(IConfigurationProfileService.ProfileModeEnvironmentVariable)?.Trim(),
                "always", StringComparison.OrdinalIgnoreCase);

            var fingerprint = ComputeFingerprint(source, profile);

            if (!alwaysApply && ReadAppliedFingerprint() == fingerprint)
            {
                _logger.Log($"Configuration profile '{source}' was already applied, skipping.");
                return false;
            }

            _logger.Log($"Applying configuration profile from '{source}'...");
            await ImportAsync(profile, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
                WriteAppliedFingerprint(fingerprint);

            _logger.Log($"Configuration profile from '{source}' applied.");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            // A broken profile must never stop the IDE from starting.
            _logger.Error($"Failed to apply configuration profile from '{source}': {e.Message}", e);
            return false;
        }
    }

    private static bool IsHttpUrl(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static ConfigurationProfile Deserialize(string content)
    {
        var profile = JsonSerializer.Deserialize<ConfigurationProfile>(content, SerializerOptions);
        return profile ?? throw new InvalidOperationException("Failed to deserialize configuration profile.");
    }

    private string AppliedMarkerPath => Path.Combine(_paths.AppDataDirectory, AppliedMarkerFileName);

    /// <summary>
    /// Identifies a profile by source and content, so both editing the profile in place and
    /// pointing the variable at a different profile trigger a re-apply.
    /// </summary>
    private static string ComputeFingerprint(string source, ConfigurationProfile profile)
    {
        var payload = source + "\n" + JsonSerializer.Serialize(profile, SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private string? ReadAppliedFingerprint()
    {
        try
        {
            return File.Exists(AppliedMarkerPath) ? File.ReadAllText(AppliedMarkerPath).Trim() : null;
        }
        catch (Exception e)
        {
            _logger.Warning($"Failed to read configuration profile marker: {e.Message}");
            return null;
        }
    }

    private void WriteAppliedFingerprint(string fingerprint)
    {
        try
        {
            Directory.CreateDirectory(_paths.AppDataDirectory);
            File.WriteAllText(AppliedMarkerPath, fingerprint);
        }
        catch (Exception e)
        {
            // Losing the marker only means the profile is applied again next launch.
            _logger.Warning($"Failed to persist configuration profile marker: {e.Message}");
        }
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
                if(!_packageService.Packages.TryGetValue(packageEntry.Id, out var existingState)) continue;
                
                // Skip if already installed
                if (existingState.InstalledVersion != null)
                {
                    _logger.Log($"Package '{packageEntry.Id}' is already installed, skipping.");
                    continue;
                }

                PackageVersion? targetVersion = null;
                if (packageEntry.Version != null)
                {
                    targetVersion =
                        existingState.Package.Versions?.FirstOrDefault(x => x.Version == packageEntry.Version);
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
