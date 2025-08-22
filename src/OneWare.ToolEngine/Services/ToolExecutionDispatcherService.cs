using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ToolEngine.Strategies;

namespace OneWare.ToolEngine.Services;

public class ToolExecutionDispatcherService : IToolExecutionDispatcherService
{
    private readonly Dictionary<string, Dictionary<string, IToolExecutionStrategy>> _toolStrategies = new();


    public void Register(string toolKey, IToolExecutionStrategy strategy)
    {
        if (!_toolStrategies.TryGetValue(toolKey, out var strategyMap))
        {
            strategyMap = new Dictionary<string, IToolExecutionStrategy>();
            _toolStrategies[toolKey] = strategyMap;
        }
        
        // TODO: IDK if this makes sense if the strategy has the key in itself: Think about it
        strategyMap[strategy.GetStrategyKey()] = strategy;
    }

    public void Register(string toolKey)
    {
        Register(toolKey, new NativeStrategy());
    }

    public void Unregister(string strategyKey)
    {
        foreach (var toolEntry in _toolStrategies)
        {
            var strategyMap = toolEntry.Value;
            strategyMap.Remove(strategyKey);
        }
    }

    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command, ToolConfiguration configuration)
    {
        var strategyKey = configuration.StrategyMapping[command.ToolName];
        return ExecuteAsync(command, strategyKey);
    }

    // TODO: Trennen von std:out & std:err 
    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command, string strategyKey)
    {
        if (_toolStrategies.TryGetValue(command.ToolName, out var strategies) &&
            strategies.TryGetValue(strategyKey, out var strategy))
        {
            return strategy.ExecuteAsync(command);
        }
        
        throw new InvalidOperationException(
            $"No execution strategy found for tool '{command.ToolName}' and strategy '{strategyKey}'");
    }
    
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey)
    {
        return _toolStrategies[toolKey].Values.ToList();
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
}