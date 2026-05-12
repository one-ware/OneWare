using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;

namespace OneWare.ToolEngine.Services;

public class ToolExecutionDispatcherService(IToolService service, ILogger logger) : IToolExecutionDispatcherService
{
    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command)
    {
        try
        {
            return service.GetStrategy(command.ToolName).ExecuteAsync(command);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, exception.Message);
        }
        
        return Task.FromResult<(bool success, string output)>((false, ""));
        
    }

    public WeakReference<Process> StartWeakProcess(ToolCommand command)
    {
        try
        {
            return service.GetStrategy(command.ToolName).StartWeakProcess(command);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, exception.Message);
        }
        
        return null!;
    }

    public IToolCommandBuilder CreateToolCommandBuilder(string toolName)
    {
        // This is just in case you want to access a different service in the Builder in the future.
        // Such as the outputService or similar.
        return new ToolCommandBuilder(toolName);
    }
}