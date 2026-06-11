using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

/// <summary>
/// Service for exporting and importing IDE configuration profiles.
/// </summary>
public interface IConfigurationProfileService
{
    /// <summary>
    /// Exports the current IDE state (settings, installed packages, package sources) to a profile.
    /// </summary>
    Task<ConfigurationProfile> ExportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a configuration profile, applying settings and installing packages.
    /// </summary>
    Task ImportAsync(ConfigurationProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a configuration profile to a file.
    /// </summary>
    Task SaveToFileAsync(ConfigurationProfile profile, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a configuration profile from a file.
    /// </summary>
    Task<ConfigurationProfile> LoadFromFileAsync(string path, CancellationToken cancellationToken = default);
}
