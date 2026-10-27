namespace OneWare.Essentials.PackageManager;

/// <summary>
///     Another package that must be installed for a <see cref="PackageVersion" /> to work,
///     for example a plugin that extends another plugin.
/// </summary>
public class PackageDependency
{
    /// <summary>
    ///     ID of the required package.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    ///     Lowest version of the required package that satisfies the dependency (inclusive).
    ///     When empty, any version satisfies it.
    /// </summary>
    public string? MinVersion { get; init; }
}
