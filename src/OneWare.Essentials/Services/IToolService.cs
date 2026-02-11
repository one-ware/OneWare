using System.Collections.ObjectModel;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolService
{
    /// <summary>
    /// Registers a tool and its default strategy.
    /// </summary>
    void Register(ToolContext description, IToolExecutionStrategy strategy);

    /// <summary>
    /// Unregisters a tool by context.
    /// </summary>
    void Unregister(ToolContext description);

    /// <summary>
    /// Unregisters a tool by key.
    /// </summary>
    void Unregister(string toolKey);

    /// <summary>
    /// Returns all registered tools.
    /// </summary>
    ObservableCollection<ToolContext> GetAllTools();

    /// <summary>
    /// Returns the global tool configuration.
    /// </summary>
    ToolConfiguration GetGlobalToolConfiguration();

    /// <summary>
    /// Registers an execution strategy for a tool.
    /// </summary>
    public void RegisterStrategy(string toolKey, IToolExecutionStrategy strategy);

    /// <summary>
    /// Unregisters an execution strategy by key.
    /// </summary>
    public void UnregisterStrategy(string strategyKey);

    /// <summary>
    /// Returns all strategies for a tool.
    /// </summary>
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey);

    /// <summary>
    /// Returns strategy keys for a tool.
    /// </summary>
    public string[] GetStrategyKeys(string toolKey);

    /// <summary>
    /// Returns the active strategy for a tool.
    /// </summary>
    IToolExecutionStrategy GetStrategy(string toolKey);
}
