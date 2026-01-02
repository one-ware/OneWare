using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IToolService
{
    void Register(EnvironmentDescription description);
    
    void Unregister(EnvironmentDescription description);
    
    void Unregister(string toolKey);
    
    IReadOnlyList<EnvironmentDescription> GetAllTools(); 
    
    ToolConfiguration GetGlobalToolConfiguration();
    
    public void RegisterStrategy(string toolKey, IToolExecutionStrategy strategy);
    
    public void RegisterStrategyForGroups(IToolExecutionStrategy strategy, List<string> tags);
    
    public void UnregisterStrategy(string strategyKey);
    
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey);
    
    public string[] GetStrategyKeys(string toolKey);

    IToolExecutionStrategy GetStrategy(string toolKey);
}