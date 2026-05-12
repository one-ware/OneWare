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
    
    public IToolCommandBuilder CreateToolCommandBuilder(string toolName);
}
