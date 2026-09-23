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
}
