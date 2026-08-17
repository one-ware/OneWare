namespace OneWare.Essentials.Models;

/// <summary>
/// The steps a configuration profile import is split into.
/// </summary>
public enum ConfigurationImportStep
{
    /// <summary>
    /// Applying settings and package sources.
    /// </summary>
    Settings,

    /// <summary>
    /// Installing the packages referenced by the profile.
    /// </summary>
    Packages
}

/// <summary>
/// The state of a single <see cref="ConfigurationImportStep"/>.
/// </summary>
public enum ConfigurationImportStatus
{
    /// <summary>
    /// The step has not been started yet.
    /// </summary>
    Pending,

    /// <summary>
    /// The step is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// The step finished successfully.
    /// </summary>
    Completed,
}

/// <summary>
/// Progress information reported while a configuration profile is imported.
/// </summary>
/// <param name="Step">The step the report belongs to.</param>
/// <param name="Status">The current status of <paramref name="Step"/>.</param>
public readonly record struct ConfigurationImportProgress(
    ConfigurationImportStep Step,
    ConfigurationImportStatus Status)
{
    /// <summary>
    /// Optional description of what the step is currently doing, e.g. the package being installed.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Optional progress of the step between 0 and 1. <c>null</c> means indeterminate.
    /// </summary>
    public double? Value { get; init; }
}
