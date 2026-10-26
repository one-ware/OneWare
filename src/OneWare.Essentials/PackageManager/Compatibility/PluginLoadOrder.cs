namespace OneWare.Essentials.PackageManager.Compatibility;

/// <summary>
///     Describes a plugin folder for <see cref="PluginLoadOrder" />.
/// </summary>
/// <param name="Path">Plugin folder.</param>
/// <param name="RequiredAssemblies">Assembly names listed in the plugin's <c>compatibility.txt</c>.</param>
/// <param name="ProvidedAssemblies">Names of the managed assemblies the plugin ships.</param>
public sealed record PluginLoadInfo(
    string Path,
    IReadOnlyCollection<string> RequiredAssemblies,
    IReadOnlyCollection<string> ProvidedAssemblies);

/// <summary>
///     Orders plugins so a plugin that requires an assembly shipped by another plugin is loaded after it.
///     The required plugin's assemblies must be loaded first, otherwise the dependent plugin's types cannot be
///     resolved and its compatibility cannot be checked.
/// </summary>
public static class PluginLoadOrder
{
    /// <summary>
    ///     Returns the plugins in load order. Independent plugins keep their relative order,
    ///     plugins in a dependency cycle are appended in their original order.
    /// </summary>
    public static IReadOnlyList<PluginLoadInfo> Sort(IReadOnlyList<PluginLoadInfo> plugins)
    {
        var providers = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < plugins.Count; i++)
            foreach (var assembly in plugins[i].ProvidedAssemblies)
            {
                if (!providers.TryGetValue(assembly, out var list))
                {
                    list = [];
                    providers[assembly] = list;
                }

                list.Add(i);
            }

        var dependents = new List<int>[plugins.Count];
        var indegree = new int[plugins.Count];
        for (var i = 0; i < plugins.Count; i++) dependents[i] = [];

        for (var i = 0; i < plugins.Count; i++)
        {
            var requiredFrom = new HashSet<int>();
            foreach (var assembly in plugins[i].RequiredAssemblies)
            {
                if (!providers.TryGetValue(assembly, out var list)) continue;

                // A plugin that ships the assembly itself does not depend on another provider
                if (list.Contains(i)) continue;

                foreach (var provider in list) requiredFrom.Add(provider);
            }

            foreach (var provider in requiredFrom)
            {
                dependents[provider].Add(i);
                indegree[i]++;
            }
        }

        var ordered = new List<PluginLoadInfo>(plugins.Count);
        var done = new bool[plugins.Count];

        // Picking the first ready plugin each round keeps the original order where possible
        bool progress;
        do
        {
            progress = false;
            for (var i = 0; i < plugins.Count; i++)
            {
                if (done[i] || indegree[i] > 0) continue;

                done[i] = true;
                ordered.Add(plugins[i]);
                foreach (var dependent in dependents[i]) indegree[dependent]--;
                progress = true;
                break;
            }
        } while (progress);

        for (var i = 0; i < plugins.Count; i++)
            if (!done[i])
                ordered.Add(plugins[i]);

        return ordered;
    }
}
