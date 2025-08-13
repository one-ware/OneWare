using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IToolExecutionStrategy
{
    Task<(bool success, string output)> ExecuteAsync(ToolCommand command);
}