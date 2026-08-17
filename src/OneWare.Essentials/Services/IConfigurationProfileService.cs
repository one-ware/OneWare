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
    /// Imports a configuration profile, reporting the progress of each import step.
    /// </summary>
    /// <param name="profile">The profile to import.</param>
    /// <param name="progress">
    /// Receives a report whenever a step starts, advances or finishes. Create it as
    /// <see cref="Progress{T}"/> on the UI thread to get the callbacks marshalled back automatically.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the import.</param>
    Task ImportAsync(ConfigurationProfile profile, IProgress<ConfigurationImportProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a configuration profile to a file.
    /// </summary>
    Task SaveToFileAsync(ConfigurationProfile profile, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a configuration profile from a file.
    /// </summary>
    Task<ConfigurationProfile> LoadFromFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a configuration profile from a local file path or an <c>http(s)</c> URL.
    /// </summary>
    Task<ConfigurationProfile> LoadFromSourceAsync(string source, CancellationToken cancellationToken = default);
}
