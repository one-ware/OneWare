using System.Diagnostics;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolExecutionDispatcherService
{
    /// <summary>
    /// Executes a tool command using the configured execution strategy.
    /// </summary>
    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command);
    
    /// <summary>
    ///     Starts the tool as a background process without waiting for its completion or capturing its output.
    /// </summary>
    /// <param name="command">The run configuration for the strategy.</param>
    /// <returns>A <see cref="WeakReference{Process}"/> to the started process, allowing it to be garbage collected if no other references exist.</returns>
    public WeakReference<Process> StartWeakProcess(ToolCommand command);

    /// <summary>
    ///     Starts the tool as a long-running background process via its configured execution strategy
    ///     (or the strategy named by <see cref="ToolCommand.ForcedStrategyKey"/>, if set).
    ///     The process is tracked internally until it is stopped with <see cref="StopProcess"/> or exits on its own.
    /// </summary>
    /// <param name="command">The run configuration for the strategy.</param>
    /// <returns>
    ///     An opaque handle identifying the started process, to be passed to <see cref="StopProcess"/>.
    ///     <see cref="Guid.Empty"/> indicates the process could not be started - e.g. the tool isn't
    ///     registered, or a forced strategy key doesn't match any strategy registered for the tool.
    /// </returns>
    public Guid StartProcess(ToolCommand command);

    /// <summary>
    ///     Stops a background process previously started with <see cref="StartProcess"/>.
    ///     The handle is routed back to whichever strategy created it, regardless of the tool's current strategy setting.
    /// </summary>
    /// <param name="handle">The handle returned by <see cref="StartProcess"/>.</param>
    /// <returns><c>true</c> if a running process was found for the handle and stopped; otherwise <c>false</c>.</returns>
    public bool StopProcess(Guid handle);

    /// <summary>
    /// Creates a new instance of <see cref="IToolCommandBuilder"/> for a specific tool.
    /// This is the entry point for configuring a tool command with specific arguments, environment variables, and mappings.
    /// </summary>
    /// <param name="toolName">The name of the tool to be executed (e.g., "yosys", "gcc").
    /// Used for logging and identifying the executable if no path is provided.</param>
    /// <returns>A fluent builder instance to configure the command.</returns>
    public IToolCommandBuilder CreateToolCommandBuilder(string toolName);
}
