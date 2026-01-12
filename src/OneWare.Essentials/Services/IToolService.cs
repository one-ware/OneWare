using System.Collections.ObjectModel;
using OneWare.Essentials.Models;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolService
{
    void Register(ToolContext description, IToolExecutionStrategy strategy);
    
    void Unregister(ToolContext description);
    
    void Unregister(string toolKey);
    
    ObservableCollection<ToolContext> GetAllTools(); 
    
    ToolConfiguration GetGlobalToolConfiguration();
    
    public void RegisterStrategy(string toolKey, IToolExecutionStrategy strategy);
    
    public void UnregisterStrategy(string strategyKey);
    
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey);
    
    public string[] GetStrategyKeys(string toolKey);

    IToolExecutionStrategy GetStrategy(string toolKey);
}