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
    /// Removes a previously installed plugin.
    /// </summary>
    public void RemovePlugin(IPlugin plugin);
}
