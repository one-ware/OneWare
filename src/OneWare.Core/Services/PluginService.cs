using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.Core.Services;

public class PluginService : IPluginService
{
    private List<IPlugin> _plugins = new();
    
    public IEnumerable<IPlugin> Plugins => _plugins;

    public PluginService()
    {
        
    }
    
    public void AddPlugin(string path)
    {
        
    }

    public void RemovePlugin(IPlugin plugin)
    {
        
    }
}