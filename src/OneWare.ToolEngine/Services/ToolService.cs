using System.Collections.ObjectModel;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using Splat;
using ILogger = OneWare.Essentials.Services.ILogger;

namespace OneWare.ToolEngine.Services;

public class ToolService : IToolService
{
    private readonly ObservableCollection<ToolContext> _tools = new();
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, Dictionary<string, IToolExecutionStrategy>> _toolStrategies = new();
    
    public ToolService(ISettingsService settingsService, ILogger logger)
    {
        _settingsService = settingsService 
                           ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private void RegisterToolInSettings(ToolContext description)
    {
        var strategies = GetStrategyKeys(description.Key);
        
        if (strategies.Length == 0) {
            _logger.Warning($"No strategies found for Tool: {description.Key}");
            return;
        }
        
        var setting = new ComboBoxSetting(description.Name, strategies[0], strategies.ToArray());
        _settingsService.RegisterSetting("Binary Management", "Execution Strategy", description.Key, setting);
    }
    
    public void Register(ToolContext description, IToolExecutionStrategy strategy)
    {
        RegisterStrategy(description.Key, strategy);
        RegisterToolInSettings(description);
        
        _tools.Add(description);
    }
     

    public void Unregister(ToolContext description)
    {
        _tools.Remove(description);
    }

    public void Unregister(string toolKey)
    {
        var tool = _tools.FirstOrDefault(t => t.Key == toolKey);
        
        if (tool is null)
        {
            throw new InvalidOperationException($"Tool with key '{toolKey}' not found.");
        }
        
        Unregister(tool);
    }

    public ObservableCollection<ToolContext> GetAllTools()
    {
        return _tools;
    }

    public ToolConfiguration GetGlobalToolConfiguration()
    {
        var config = new ToolConfiguration();
        foreach (var tool in _tools)
        {
            config.StrategyMapping.Add(tool.Key, _settingsService.GetSettingValue<string>(tool.Key));
        }
        return config;
    }

    public void UpdateSettings()
    {
        
    }
    
    public void RegisterStrategy(string toolKey, IToolExecutionStrategy strategy)
    {
        if (!_toolStrategies.TryGetValue(toolKey, out var strategyMap))
        {
            strategyMap = new Dictionary<string, IToolExecutionStrategy>();
            _toolStrategies[toolKey] = strategyMap;
        }
        
        strategyMap[strategy.GetStrategyKey()] = strategy;
    }

    public void UnregisterStrategy(string strategyKey)
    {
        foreach (var toolEntry in _toolStrategies)
        {
            var strategyMap = toolEntry.Value;
            strategyMap.Remove(strategyKey);
        }
    }
    
    public string[] GetStrategyKeys(string toolKey)
    {
        if (_toolStrategies.TryGetValue(toolKey, out var strategies))
        {
            return strategies.Values
                .Select(s => s.GetStrategyKey())  
                .ToArray();
        }

        return [];
    }
    
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey)
    {
        return _toolStrategies[toolKey].Values.ToList();
    }

    public IToolExecutionStrategy GetStrategy(string toolKey)
    {
        var strategyKey = _settingsService.GetSettingValue<string>(toolKey);
        if (_toolStrategies.TryGetValue(toolKey, out var strategies) &&
            strategies.TryGetValue(strategyKey, out var strategy))
        {
            return strategy;
        }
        
        throw new InvalidOperationException(
            $"No execution strategy found for tool '{toolKey}' and strategy '{strategyKey}'");
    }
}