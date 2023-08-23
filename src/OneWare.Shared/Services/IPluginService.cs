using OneWare.Shared.Models;

namespace OneWare.Shared.Services;

public interface IPluginService
{
    public IEnumerable<string> InstalledPlugins { get; }
    
    public void AddPlugin(string path);

    public void RemovePlugin(string plugin);
}