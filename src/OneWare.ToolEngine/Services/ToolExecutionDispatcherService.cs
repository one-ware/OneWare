using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;

namespace OneWare.ToolEngine.Services;

public class ToolExecutionDispatcherService(IToolService service, ILogger logger) : IToolExecutionDispatcherService
{
    private readonly ConcurrentDictionary<Guid, IToolExecutionStrategy> _backgroundProcessStrategies = new();

    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command)
    {
        try
        {
            return ResolveStrategy(command).ExecuteAsync(command);
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
            return ResolveStrategy(command).StartWeakProcess(command);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, exception.Message);
        }

        return null!;
    }

    public Guid StartProcess(ToolCommand command)
    {
        try
        {
            var strategy = ResolveStrategy(command);
            var handle = strategy.StartProcess(command);
            _backgroundProcessStrategies[handle] = strategy;
            return handle;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, exception.Message);
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Resolves the strategy for a command: the one named by <see cref="ToolCommand.ForcedStrategyKey"/> if
    /// set, otherwise the tool's currently configured strategy. Throws <see cref="InvalidOperationException"/>
    /// if a forced key doesn't match any strategy registered for the tool, consistent with how an unregistered
    /// tool is handled by <see cref="IToolService.GetStrategy"/>.
    /// </summary>
    private IToolExecutionStrategy ResolveStrategy(ToolCommand command)
    {
        if (command.ForcedStrategyKey is { } forcedKey)
            return service.TryGetStrategy(command.ToolName, forcedKey)
                   ?? throw new InvalidOperationException(
                       $"Forced strategy '{forcedKey}' is not registered for tool '{command.ToolName}'.");

        return service.GetStrategy(command.ToolName);
    }

    public bool StopProcess(Guid handle)
    {
        if (!_backgroundProcessStrategies.TryRemove(handle, out var strategy)) return false;

        return strategy.StopProcess(handle);
    }

    public IToolCommandBuilder CreateToolCommandBuilder(string toolName)
    {
        // This is just in case you want to access a different service in the Builder in the future.
        // Such as the outputService or similar.
        return new ToolCommandBuilder(toolName);
    }
}