using OneWare.Essentials.Models;

namespace OneWare.Essentials.PackageManager;

public static class PackageStateExtensions
{
    /// <summary>
    /// The version that installing or updating this package applies: the newest version this Studio
    /// build supports, on the release channel the package is installed from. Prereleases are only
    /// offered when the installed version is a prerelease itself, so a stable installation is never
    /// moved onto the prerelease channel by an update.
    /// </summary>
    /// <remarks>
    /// This is the single source of truth for "which version does the update button install", used
    /// by the package status, the package manager and the Studio updater alike.
    /// </remarks>
    public static PackageVersion? ResolveTargetVersion(this IPackageState state, bool includePrerelease = false)
    {
        return state.Package.Versions?
            .Where(x => (includePrerelease || !x.IsPrerelease) && x.IsSupportedByStudio())
            .OrderByDescending(x => SemanticVersion.TryParse(x.Version, out var parsed) ? parsed : SemanticVersion.Empty)
            .FirstOrDefault();
    }

    /// <summary>
    /// The stable version a bulk update ("Update All", Studio updater) moves this package to, or
    /// <c>null</c> when there is none. Prereleases never count, and the target must be strictly newer
    /// than the installed version, so a package whose only update is a prerelease is skipped.
    /// </summary>
    public static PackageVersion? ResolveStableUpdateVersion(this IPackageState state)
    {
        if (!SemanticVersion.TryParse(state.InstalledVersion?.Version, out var installed))
            return null;

        var target = state.ResolveTargetVersion();

        return SemanticVersion.TryParse(target?.Version, out var targetVersion) && targetVersion > installed
            ? target
            : null;
    }
}
