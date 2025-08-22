using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IToolExecutionDispatcherService
{
    public void Register(string toolKey, IToolExecutionStrategy strategy);
    
    public void Register(string toolKey);
    
    public void Unregister(string strategyKey);

    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command, ToolConfiguration configuration);
    
    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command, string strategyKey);
    
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey);

    public string[] GetStrategyKeys(string toolKey);
}