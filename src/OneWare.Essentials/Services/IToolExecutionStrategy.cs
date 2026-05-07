using System.Diagnostics;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolExecutionStrategy
{
    /// <summary>
    ///     Executes the ToolCommand in a strategy dependent manner
    /// </summary>
    /// <param name="command">The run configuration for the strategy</param>
    /// <returns></returns>
    Task<(bool success, string output)> ExecuteAsync(ToolCommand command);
    
    
    /// <summary>
    ///     Starts the tool as a background process without waiting for its completion or capturing its output.
    /// </summary>
    /// <param name="command">The run configuration for the strategy.</param>
    /// <returns>A <see cref="WeakReference{Process}"/> to the started process, allowing it to be garbage collected if no other references exist.</returns>
    public WeakReference<Process> StartWeakProcess(ToolCommand command);

    /// <summary>
    ///     Returns the display name for a strategy.
    ///     Is used in settings vies
    /// </summary>
    /// <returns></returns>
    string GetStrategyName();

    /// <summary>
    ///     Returns the unique key for a strategy.
    /// </summary>
    /// <returns></returns>
    string GetStrategyKey();
}
