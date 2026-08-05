using System.Collections.Concurrent;
using System.Diagnostics;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.ToolEngine.Strategies;

public class NativeStrategy : IToolExecutionStrategy
{
    public const string ToolKey = "NativeExecutionStrategy";

    private readonly ConcurrentDictionary<Guid, Process> _backgroundProcesses = new();

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

    public Guid StartProcess(ToolCommand command)
    {
        var childProcessService = ContainerLocator.Container.Resolve<IChildProcessService>();
        var process = childProcessService.StartProcess(command.Executable ?? command.ToolName, command.Arguments,
            command.WorkingDirectory);

        var handle = Guid.NewGuid();
        _backgroundProcesses[handle] = process;

        process.Exited += (_, _) =>
        {
            _backgroundProcesses.TryRemove(handle, out _);
            process.Dispose();
        };

        return handle;
    }

    public bool StopProcess(Guid handle)
    {
        if (!_backgroundProcesses.TryRemove(handle, out var process)) return false;

        try
        {
            if (!process.HasExited) process.Kill(true);
            return true;
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the check and the kill call.
            return true;
        }
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