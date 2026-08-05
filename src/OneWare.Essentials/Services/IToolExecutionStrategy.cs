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
    ///     Starts the tool as a long-running background process that is tracked by the strategy until it is
    ///     explicitly stopped via <see cref="StopProcess"/> or exits on its own.
    /// </summary>
    /// <param name="command">The run configuration for the strategy.</param>
    /// <returns>An opaque handle identifying the started process.</returns>
    public Guid StartProcess(ToolCommand command);

    /// <summary>
    ///     Stops a background process previously started with <see cref="StartProcess"/>.
    /// </summary>
    /// <param name="handle">The handle returned by <see cref="StartProcess"/>.</param>
    /// <returns><c>true</c> if a running process was found for the handle and stopped; otherwise <c>false</c>.</returns>
    public bool StopProcess(Guid handle);

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
