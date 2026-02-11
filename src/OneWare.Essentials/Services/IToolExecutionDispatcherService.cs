using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolExecutionDispatcherService
{
    /// <summary>
    /// Executes a tool command using the configured execution strategy.
    /// </summary>
    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command);
}
