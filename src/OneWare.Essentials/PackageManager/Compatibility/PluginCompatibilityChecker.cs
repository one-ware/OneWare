using System.Reflection;
namespace OneWare.Essentials.PackageManager.Compatibility;

/// <summary>
///     Checks the <see cref="PluginManifest" /> of a plugin against OneWare Studio and the loaded plugins.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             OneWare assemblies (<c>OneWare.*</c>) and assemblies provided by other plugins must have the same
///             major version and an equal or newer version. With calendar versioning the major version is the year,
///             so a plugin built for 2027.1 runs on every 2027.x release from 2027.1 on.
///         </item>
///         <item>Other (third-party) assemblies only need to be present, their version is not checked.</item>
///         <item>A plugin without a valid manifest is not compatible.</item>
///     </list>
/// </remarks>
public static class PluginCompatibilityChecker
{
    /// <summary>
    ///     Checks the <c>oneware-plugin.json</c> of the plugin at <paramref name="path" />.
    /// </summary>
    /// <param name="path">Plugin folder.</param>
    /// <param name="pluginAssemblies">
    ///     Assemblies provided by already loaded plugins. A plugin that extends another plugin requires
    ///     assemblies that are not part of the core application.
    /// </param>
    public static CompatibilityReport CheckCompatibilityPath(string path,
        IReadOnlyDictionary<string, Version> pluginAssemblies)
    {
        var manifest = PluginManifest.Load(path);
        return manifest == null ? InvalidManifest() : CheckCompatibility(manifest, pluginAssemblies);
    }

    /// <summary>
    ///     Checks the content of an <c>oneware-plugin.json</c>.
    /// </summary>
    public static CompatibilityReport CheckCompatibility(string? manifestJson,
        IReadOnlyDictionary<string, Version> pluginAssemblies)
    {
        return PluginManifest.TryParse(manifestJson, out var manifest)
            ? CheckCompatibility(manifest!, pluginAssemblies)
            : InvalidManifest();
    }

    public static CompatibilityReport CheckCompatibility(PluginManifest manifest,
        IReadOnlyDictionary<string, Version> pluginAssemblies)
    {
        var hostAssemblies = GetReferencedAssembliesRecursive(Assembly.GetEntryAssembly()!)
            .Where(x => x.Value.Version != null)
            .ToDictionary(x => x.Key, x => x.Value.Version!, StringComparer.OrdinalIgnoreCase);

        return CheckCompatibility(manifest, hostAssemblies, pluginAssemblies);
    }

    internal static CompatibilityReport CheckCompatibility(PluginManifest manifest,
        IReadOnlyDictionary<string, Version> hostAssemblies, IReadOnlyDictionary<string, Version> pluginAssemblies)
    {
        if (manifest.ManifestVersion > PluginManifest.SupportedManifestVersion)
            return new CompatibilityReport(false, [
                new CompatibilityIssue(PluginManifest.FileName, new Version(manifest.ManifestVersion, 0),
                    new Version(PluginManifest.SupportedManifestVersion, 0), CompatibilityRecordKind.RequiresCoreUpdate)
            ]);

        var issues = new List<CompatibilityIssue>();
        var isCompatible = true;

        foreach (var (name, required) in manifest.Dependencies)
        {
            Version? provided;
            bool checkVersion;

            if (hostAssemblies.TryGetValue(name, out var hostVersion))
            {
                provided = hostVersion;
                checkVersion = IsOneWareAssembly(name);
            }
            else if (pluginAssemblies.TryGetValue(name, out var pluginVersion))
            {
                provided = pluginVersion;
                checkVersion = true;
            }
            else
            {
                issues.Add(new CompatibilityIssue(name, required, null, CompatibilityRecordKind.MissingDependency));
                isCompatible = false;
                continue;
            }

            if (!checkVersion) continue;

            provided = NormalizeVersion(provided);
            var comparison = provided.CompareTo(required);

            if (provided.Major != required.Major || comparison < 0)
            {
                issues.Add(new CompatibilityIssue(name, required, provided,
                    comparison < 0 ? CompatibilityRecordKind.RequiresCoreUpdate : CompatibilityRecordKind.PluginOutdated));
                isCompatible = false;
            }
            else if (comparison > 0)
            {
                issues.Add(new CompatibilityIssue(name, required, provided, CompatibilityRecordKind.PluginOutdated));
            }
        }

        return new CompatibilityReport(isCompatible, issues);
    }

    /// <summary>
    ///     Assemblies of OneWare Studio itself. Their version is checked, other host assemblies only need to be present.
    /// </summary>
    public static bool IsOneWareAssembly(string assemblyName)
    {
        return assemblyName.StartsWith("OneWare.", StringComparison.OrdinalIgnoreCase);
    }

    private static CompatibilityReport InvalidManifest()
    {
        return new CompatibilityReport(false, [
            new CompatibilityIssue(PluginManifest.FileName, null, null, CompatibilityRecordKind.InvalidManifest)
        ]);
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(Math.Max(version.Major, 0), Math.Max(version.Minor, 0), Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }

    public static Dictionary<string, AssemblyName> GetReferencedAssembliesRecursive(Assembly rootAssembly)
    {
        var result = new Dictionary<string, AssemblyName>();
        var toVisit = new Queue<AssemblyName>(rootAssembly.GetReferencedAssemblies());

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();

            if (result.ContainsKey(current.Name!))
                continue;

            result[current.Name!] = current;

            // Try to find the physical assembly to inspect further
            try
            {
                var loadedAsm = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == current.Name);

                if (loadedAsm != null)
                {
                    var referenced = loadedAsm.GetReferencedAssemblies();
                    foreach (var refAsm in referenced)
                        if (!result.ContainsKey(refAsm.Name!))
                            toVisit.Enqueue(refAsm);
                }
                // Else: it's referenced but not loaded and we stop here
            }
            catch
            {
                // Ignore load errors
            }
        }

        return result;
    }
}
