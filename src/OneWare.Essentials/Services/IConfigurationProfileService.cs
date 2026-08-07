using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

/// <summary>
/// Service for exporting and importing IDE configuration profiles.
/// </summary>
public interface IConfigurationProfileService
{
    /// <summary>
    /// Environment variable holding the profile to apply at startup. The value is either a local
    /// file path or an <c>http(s)</c> URL.
    /// </summary>
    public const string ProfileEnvironmentVariable = "ONEWARE_CONFIGURATION_PROFILE";

    /// <summary>
    /// Environment variable controlling how often the profile from
    /// <see cref="ProfileEnvironmentVariable"/> is applied. <c>once</c> (default) applies a profile
    /// only when its content has not been applied before; <c>always</c> re-applies on every launch.
    /// </summary>
    public const string ProfileModeEnvironmentVariable = "ONEWARE_CONFIGURATION_PROFILE_MODE";

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

    /// <summary>
    /// Loads a configuration profile from a local file path or an <c>http(s)</c> URL.
    /// </summary>
    Task<ConfigurationProfile> LoadFromSourceAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the profile referenced by <see cref="ProfileEnvironmentVariable"/>, if set.
    /// </summary>
    /// <remarks>
    /// Intended for deployment scenarios where an installation script provisions the variable
    /// (directly or through <c>OneWareStudio.defaults.json</c>). Failures are logged rather than
    /// thrown so a bad profile can never prevent the IDE from starting.
    /// </remarks>
    /// <returns><see langword="true"/> if a profile was applied.</returns>
    Task<bool> ApplyEnvironmentProfileAsync(CancellationToken cancellationToken = default);
}
