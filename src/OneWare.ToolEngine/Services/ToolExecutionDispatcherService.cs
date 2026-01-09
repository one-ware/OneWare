using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ToolEngine.Strategies;

namespace OneWare.ToolEngine.Services;

public class ToolExecutionDispatcherService(IToolService service) : IToolExecutionDispatcherService
{
    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command)
    {
        return service.GetStrategy(command.ToolName).ExecuteAsync(command);
    }
    
}