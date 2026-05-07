using System.Diagnostics;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.ToolEngine.Strategies;

public class NativeStrategy : IToolExecutionStrategy
{
    public const string ToolKey = "NativeExecutionStrategy";

    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command)
    {
        var childProcessService = ContainerLocator.Container.Resolve<IChildProcessService>();
        return childProcessService.ExecuteShellAsync(command.Executable ?? command.ToolName, command.Arguments, command.WorkingDirectory, command.StatusMessage, 
            command.State,
            command.ShowTimer, command.OutputHandler, command.ErrorHandler);
    }

    public WeakReference<Process> StartWeakProcess(ToolCommand command)
    {
        var childProcessService = ContainerLocator.Container.Resolve<IChildProcessService>();
        return childProcessService.StartWeakProcess(command.Executable ?? command.ToolName, command.Arguments,
            command.WorkingDirectory);
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