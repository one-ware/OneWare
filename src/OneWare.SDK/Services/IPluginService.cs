using OneWare.SDK.Models;

namespace OneWare.SDK.Services;

public interface IPluginService
{
    public List<IPlugin> InstalledPlugins { get; }
    
    public IPlugin AddPlugin(string path);
    
    public void RemovePlugin(IPlugin plugin);
}