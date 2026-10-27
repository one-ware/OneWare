using System.Reflection;

namespace OneWare.Essentials.PackageManager;

public static class PackageVersionExtensions
{
    private static readonly Version? StudioVersion = Assembly.GetEntryAssembly()?.GetName().Version;

    /// <summary>
    /// Checks whether this Studio build satisfies the version's
    /// <see cref="PackageVersion.MinStudioVersion"/>. A version that requires a newer Studio must
    /// not be offered for install or update, because it cannot run on the installed Studio.
    /// </summary>
    public static bool IsSupportedByStudio(this PackageVersion version)
    {
        if (string.IsNullOrWhiteSpace(version.MinStudioVersion)) return true;

        // A requirement that cannot be read is no reason to hide the version.
        if (!Version.TryParse(version.MinStudioVersion, out var minimum)) return true;

        return StudioVersion == null || StudioVersion >= minimum;
    }

    /// <summary>
    ///     Checks whether <paramref name="version" /> satisfies the dependency's
    ///     <see cref="PackageDependency.MinVersion" />. A missing <paramref name="version" /> never satisfies it,
    ///     a version or requirement that cannot be read is treated as satisfied.
    /// </summary>
    public static bool IsSatisfiedBy(this PackageDependency dependency, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        if (!SemanticVersion.TryParse(version, out var provided)) return true;
        if (string.IsNullOrWhiteSpace(dependency.MinVersion)) return true;
        if (!SemanticVersion.TryParse(dependency.MinVersion, out var minimum)) return true;

        return provided >= minimum;
    }
}
