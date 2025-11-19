using System.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class NodeProviderRegistry(ISettingsService settingsService) : INodeProviderRegistry
{
    private readonly Dictionary<string, Dictionary<string, INodeProvider>> _providerMap = new();
    
    public void Register(string type, INodeProvider nodeProvider)
    {
        if (!_providerMap.ContainsKey(type))
        {
            _providerMap[type] = new Dictionary<string, INodeProvider>();
        }
        
        _providerMap[type][nodeProvider.GetKey()] = nodeProvider;
        UpdateSetting(type);
    }

    public void Register<TNodeProvider>(string type) where TNodeProvider : INodeProvider
    {
        Register(type, ContainerLocator.Container.Resolve<TNodeProvider>());
    }

    private void UpdateSetting(string type)
    {
        var providers = GetNodeProviders(type);
        var providerKeys = providers
            .Select(p => p.GetDisplayName()) 
            .ToArray<object>();
        
        if (settingsService.HasSetting($"{type}-node-provider"))
        {
            var setting = (ComboBoxSetting) settingsService.GetSetting($"{type}-node-provider");
            setting.Options = providerKeys;
        }
        else
        {
            var box = new ComboBoxSetting("Node Provider", providerKeys[0], providerKeys);
            settingsService.RegisterSetting("Languages", type, $"{type}-node-provider", box);    
        }
    }

    public void Unregister(string type, string nodeExporterKey)
    {
        if (_providerMap.TryGetValue(type, out var innerMap))
        {
            innerMap.Remove(nodeExporterKey);
        }
    }

    public List<INodeProvider> GetNodeProviders(string type)
    {
        return _providerMap.TryGetValue(type, out var innerMap) ? innerMap.Values.ToList() : [];
    }

    public INodeProvider GetNodeProvider(string type)
    {
        var name = settingsService.GetSettingValue<string>(type);
        var foundProvider = GetNodeProviders(type).FirstOrDefault(p => p.GetDisplayName() == name);
        return foundProvider ?? throw new KeyNotFoundException($"Could not foud NodeProvider with Name '{name}'");
    }
}