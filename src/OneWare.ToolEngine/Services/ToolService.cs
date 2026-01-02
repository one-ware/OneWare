using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ToolEngine.Strategies;
using Prism.Ioc;
using Splat;
using ILogger = OneWare.Essentials.Services.ILogger;

namespace OneWare.ToolEngine.Services;

public class ToolService : IToolService
{
    private readonly List<EnvironmentDescription> _tools = new();
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, Dictionary<string, IToolExecutionStrategy>> _toolStrategies = new();
    
    public ToolService(ISettingsService settingsService, ILogger logger)
    {
        _settingsService = settingsService 
                           ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private void RegisterToolInSettings(EnvironmentDescription description)
    {
        var dispatcherService = ContainerLocator.Container.Resolve<IToolExecutionDispatcherService>();
        // TODO: Hier mal rüber schauen 
        RegisterStrategy(description.Key, new NativeStrategy());

        var strategies = GetStrategyKeys(description.Key);
        
        if (strategies.Length == 0) {
            _logger.Warning($"No strategies found for Tool: {description.Key}");
            return;
        }
        
        var setting = new ComboBoxSetting(description.Name, strategies[0], strategies.ToArray());
        _settingsService.RegisterSetting("Binary Management", "Execution Strategy", description.Key, setting);
    }
    
    public void Register(EnvironmentDescription description)
    {
        RegisterToolInSettings(description);
        _tools.Add(description);
    }
     

    public void Unregister(EnvironmentDescription description)
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

    public IReadOnlyList<EnvironmentDescription> GetAllTools()
    {
        return _tools.AsReadOnly();
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
        
        // TODO: IDK if this makes sense if the strategy has the key in itself: Think about it
        strategyMap[strategy.GetStrategyKey()] = strategy;
    }
    
    public void RegisterStrategyForGroups(IToolExecutionStrategy strategy, List<string> tags)
    {
        // Based on Group Tag in EnvironmentDescription set Strategy
        throw new NotImplementedException();
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
                .Select(s => s.GetStrategyKey())  // nur den Key nehmen
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