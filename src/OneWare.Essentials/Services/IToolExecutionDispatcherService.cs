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
    /// Creates a new instance of <see cref="IToolCommandBuilder"/> for a specific tool.
    /// This is the entry point for configuring a tool command with specific arguments, environment variables, and mappings.
    /// </summary>
    /// <param name="toolName">The name of the tool to be executed (e.g., "yosys", "gcc").
    /// Used for logging and identifying the executable if no path is provided.</param>
    /// <returns>A fluent builder instance to configure the command.</returns>
    public IToolCommandBuilder CreateToolCommandBuilder(string toolName);
}
