using System.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

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

    public void Register<TNodeProvider>(LanguageType type) where TNodeProvider : INodeProvider
    {
        Register(type, ContainerLocator.Container.Resolve<TNodeProvider>());
    }

    private void UpdateSetting(LanguageType type)
    {
        var providers = GetNodeProviders(type);
        var providerKeys = providers
            .Select(p => p.GetDisplayName()) 
            .ToArray<object>();
        
        if (type == LanguageType.Verilog)
        {   
            if (settingsService.HasSetting("verilog-node-exporter"))
            {
                var setting = (ComboBoxSetting) settingsService.GetSetting("verilog-node-exporter");
                setting.Options = providerKeys;
            }
            else
            {
                var box = new ComboBoxSetting("Node Provider", providerKeys[0], providerKeys);
                settingsService.RegisterSetting("Languages", "Verilog", "verilog-node-exporter", box);    
            }
        } else if (type == LanguageType.Vhdl)
        {
            if (settingsService.HasSetting("vhdl-node-exporter"))
            {
                var setting = (ComboBoxSetting) settingsService.GetSetting("vhdl-node-exporter");
                setting.Options = providerKeys;
            }
            else
            {
                var box = new ComboBoxSetting("Node Provider", providerKeys[0], providerKeys);
                settingsService.RegisterSetting("Languages", "VHDL", "vhdl-node-exporter", box);    
            }
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

    public INodeProvider GetNodeProvider(LanguageType type)
    {
        var name = settingsService.GetSettingValue<string>(GetKey(type));
        var foundProvider = GetNodeProviders(type).FirstOrDefault(p => p.GetDisplayName() == name);
        return foundProvider ?? throw new KeyNotFoundException($"Could not foud NodeProvider with Name '{name}'");
    }

    private string GetKey(LanguageType type)
    {
        switch (type)
        {
            case LanguageType.Verilog:
                return "verilog-node-exporter";
            case LanguageType.Vhdl:
                return "vhdl-node-exporter";
        }

        throw new ArgumentException($"Unknown LanguageType  '{type}'");
    }
}