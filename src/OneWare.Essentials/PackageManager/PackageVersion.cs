namespace OneWare.Essentials.PackageManager;

public class PackageVersion
{
    public string? Version { get; init; }

    public bool IsPrerelease { get; init; }

    public string? MinStudioVersion { get; init; }

    public PackageTarget[]? Targets { get; init; }

    /// <summary>
    ///     URL of the plugin's <c>oneware-plugin.json</c>. Defaults to <c>{SourceUrl}/{Version}/oneware-plugin.json</c>.
    /// </summary>
    public string? ManifestUrl { get; init; }

    /// <summary>
    ///     Packages that must be installed before this version. The package manager installs missing
    ///     or outdated dependencies first.
    /// </summary>
    public PackageDependency[]? Dependencies { get; init; }
}