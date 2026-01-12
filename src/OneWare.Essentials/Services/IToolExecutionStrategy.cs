using OneWare.Essentials.Models;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolExecutionStrategy
{
    /// <summary>
    /// Executes the ToolCommand in a strategy dependent manner
    /// </summary>
    /// <param name="command">The run configuration for the strategy</param>
    /// <returns></returns>
    Task<(bool success, string output)> ExecuteAsync(ToolCommand command);

    /// <summary>
    /// Returns the display name for a strategy.
    /// Is used in settings vies 
    /// </summary>
    /// <returns></returns>
    string GetStrategyName();
    
    /// <summary>
    /// Returns the unique key for a strategy.
    /// </summary>
    /// <returns></returns>
    string GetStrategyKey();
}