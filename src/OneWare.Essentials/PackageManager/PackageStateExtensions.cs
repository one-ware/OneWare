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
    public static PackageVersion? ResolveTargetVersion(this IPackageState state)
    {
        var includePrerelease = state.InstalledVersion?.IsPrerelease ?? false;

        return state.Package.Versions?
            .Where(x => (includePrerelease || !x.IsPrerelease) && x.IsSupportedByStudio())
            .OrderByDescending(x => SemanticVersion.TryParse(x.Version, out var parsed) ? parsed : SemanticVersion.Empty)
            .FirstOrDefault();
    }
}
