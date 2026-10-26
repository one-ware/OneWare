using OneWare.Essentials.Models;

namespace OneWare.Essentials.PackageManager;

/// <summary>
///     A dependency that has to be installed, together with the version that will be installed.
/// </summary>
public sealed record PackageDependencyInstall(IPackageState State, PackageVersion Version);

/// <summary>
///     Dependencies that have to be installed before a package version, in installation order.
/// </summary>
/// <param name="ToInstall">Missing or outdated dependencies, dependencies of dependencies come first.</param>
/// <param name="Unresolved">Dependencies that no package source offers in a suitable version.</param>
public sealed record PackageDependencyPlan(
    IReadOnlyList<PackageDependencyInstall> ToInstall,
    IReadOnlyList<PackageDependency> Unresolved)
{
    public static PackageDependencyPlan Empty { get; } = new([], []);

    public bool CanInstall => Unresolved.Count == 0;
}
