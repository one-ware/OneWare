using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IToolExecutionDispatcherService
{
    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command);
}