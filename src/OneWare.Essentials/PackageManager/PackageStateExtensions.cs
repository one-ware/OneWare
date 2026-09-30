using OneWare.Essentials.Models;

namespace OneWare.Essentials.PackageManager;

public static class PackageStateExtensions
{
    /// <summary>
    /// The version that installing or updating this package applies: the newest version this Studio
    /// build supports, on the release channel the package is installed from. Prereleases are only
    /// offered when the installed version is a prerelease itself, so a stable installation is never
    /// moved onto the prerelease channel by an update. <paramref name="includePrerelease"/> forces
    /// prereleases in regardless of the installed channel.
    /// </summary>
    /// <remarks>
    /// This is the single source of truth for "which version does the update button install", used
    /// by the package status, the package manager and the Studio updater alike.
    /// </remarks>
    public static PackageVersion? ResolveTargetVersion(this IPackageState state, bool includePrerelease = false)
    {
        return ResolveNewestVersion(state, includePrerelease || state.IsOnPrereleaseChannel());
    }

    /// <summary>
    /// Whether the installed version is a prerelease, either flagged by the repository or by its
    /// semantic version suffix (the installed record does not always carry the repository flag).
    /// </summary>
    public static bool IsOnPrereleaseChannel(this IPackageState state)
    {
        var installed = state.InstalledVersion?.Version;
        if (installed == null) return false;

        if (state.InstalledVersion!.IsPrerelease) return true;
        if (state.Package.Versions?.Any(x => x.IsPrerelease && x.Version == installed) == true) return true;

        return SemanticVersion.TryParse(installed, out var parsed) && parsed.IsPrerelease;
    }

    /// <summary>
    /// The version a bulk update ("Update All", Studio updater) moves this package to, or <c>null</c>
    /// when there is none. It follows the installed channel like <see cref="ResolveTargetVersion"/>:
    /// stable installations only move to newer stable versions, prerelease installations also move to
    /// newer prereleases. The target must be strictly newer than the installed version.
    /// </summary>
    public static PackageVersion? ResolveUpdateVersion(this IPackageState state)
    {
        if (!SemanticVersion.TryParse(state.InstalledVersion?.Version, out var installed))
            return null;

        var target = state.ResolveTargetVersion();

        return SemanticVersion.TryParse(target?.Version, out var targetVersion) && targetVersion > installed
            ? target
            : null;
    }

    private static PackageVersion? ResolveNewestVersion(IPackageState state, bool includePrerelease)
    {
        return state.Package.Versions?
            .Where(x => (includePrerelease || !x.IsPrerelease) && x.IsSupportedByStudio())
            .OrderByDescending(x => SemanticVersion.TryParse(x.Version, out var parsed) ? parsed : SemanticVersion.Empty)
            .FirstOrDefault();
    }
}
