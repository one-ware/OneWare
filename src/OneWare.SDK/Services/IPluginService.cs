using OneWare.SDK.Models;

namespace OneWare.SDK.Services;

public interface IPluginService
{
    public IEnumerable<string> InstalledPlugins { get; }
    
    public void AddPlugin(string path);

    public void RemovePlugin(string plugin);
}