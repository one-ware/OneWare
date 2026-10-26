namespace OneWare.Essentials.PackageManager;

public class PackageVersion
{
    public string? Version { get; init; }

    public bool IsPrerelease { get; init; }

    public string? MinStudioVersion { get; init; }

    public PackageTarget[]? Targets { get; init; }

    public string? CompatibilityUrl { get; init; }

    /// <summary>
    ///     Packages that must be installed before this version. The package manager installs missing
    ///     or outdated dependencies first.
    /// </summary>
    public PackageDependency[]? Dependencies { get; init; }
}