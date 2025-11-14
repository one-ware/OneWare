using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class NodeProviderRegistry(ISettingsService settingsService) : INodeProviderRegistry
{
    private readonly Dictionary<LanguageType, Dictionary<string, INodeProvider>> _providerMap = new();
    
    public void Register(LanguageType type, INodeProvider nodeProvider)
    {
        if (!_providerMap.ContainsKey(type))
        {
            _providerMap[type] = new Dictionary<string, INodeProvider>();
        }
        
        _providerMap[type][nodeProvider.GetKey()] = nodeProvider;
        UpdateSetting(type);
    }

    private void UpdateSetting(LanguageType type)
    {
        if (type == LanguageType.Verilog)
        {
            List<INodeProvider> providers = GetNodeProviders(type);
            string[] providerKeys = providers
                .Select(p => p.GetDisplayName()) 
                .ToArray();
            
            var box = new ComboBoxSetting("Verilog", providerKeys[0], providerKeys);
            settingsService.RegisterSetting("Languages", "Verilog", "verilog-node-exporter", box);    
        }
        
    }

    public void Unregister(LanguageType type, string nodeExporterKey)
    {
        if (_providerMap.TryGetValue(type, out var innerMap))
        {
            innerMap.Remove(nodeExporterKey);
        }
    }

    public List<INodeProvider> GetNodeProviders(LanguageType type)
    {
        return _providerMap.TryGetValue(type, out var innerMap) ? innerMap.Values.ToList() : [];
    }
}