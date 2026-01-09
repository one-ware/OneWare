using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.ToolEngine.Strategies;

public class NativeStrategy : IToolExecutionStrategy
{
    private const string ToolKey = "NativeExecutionStrategy";

    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command)
    {
        IChildProcessService childProcessService = ContainerLocator.Container.Resolve<IChildProcessService>();
        return childProcessService.ExecuteShellAsync(command.ToolName, command.Arguments, command.WorkingDirectory, command.StatusMessage, 
            command.State,
            command.ShowTimer, command.OutputHandler, command.ErrorHandler);
    }

    public string GetStrategyName()
    {
        return "Native Execution Strategy";
    }

    public string GetStrategyKey()
    {
        return ToolKey;
    }
}