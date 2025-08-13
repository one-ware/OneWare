using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IToolExecutionDispatcherService
{
    public void Register(string toolName, IToolExecutionStrategy strategy, string? projectName = null);
    public void Unregister(string toolName, string? projectName = null);

    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command, string? projectName = null);
}