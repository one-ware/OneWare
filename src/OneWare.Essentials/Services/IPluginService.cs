using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IPluginService
{
    /// <summary>
    /// List of installed plugins.
    /// </summary>
    public List<IPlugin> InstalledPlugins { get; }

    /// <summary>
    /// Adds a plugin from a folder path.
    /// </summary>
    public IPlugin AddPlugin(string path);

    /// <summary>
    /// Adds several plugins from folder paths. Plugins that extend another plugin are loaded after it.
    /// </summary>
    public IReadOnlyList<IPlugin> AddPlugins(IEnumerable<string> paths)
    {
        return paths.Select(AddPlugin).ToList();
    }

    /// <summary>
    /// Versions of the assemblies provided by loaded plugins, keyed by assembly name.
    /// Plugins that extend another plugin are checked against them.
    /// </summary>
    public IReadOnlyDictionary<string, Version> PluginAssemblies => new Dictionary<string, Version>();

    /// <summary>
    /// Removes a previously installed plugin.
    /// </summary>
    public void RemovePlugin(IPlugin plugin);
}
