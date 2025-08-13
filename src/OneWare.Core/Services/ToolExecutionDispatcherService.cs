using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class ToolExecutionDispatcherService : IToolExecutionDispatcherService
{
    private readonly Dictionary<string, IToolExecutionStrategy> _globalStrategies = new();
    private readonly Dictionary<string, Dictionary<string, IToolExecutionStrategy>> _projectStrategies = new();
    
    public void Register(string toolName, IToolExecutionStrategy strategy, string? projectName = null)
    {
        if (projectName is null)
        {
            _globalStrategies[toolName] = strategy;
        } 
        else 
        {
            if (!_projectStrategies.ContainsKey(projectName))
            {
                _projectStrategies[projectName] = new Dictionary<string, IToolExecutionStrategy>();
            }
            _projectStrategies[projectName][toolName] = strategy;
        }
    }

    public void Unregister(string toolName, string? projectName = null)
    {
        if (projectName is null)
        {
            _globalStrategies.Remove(toolName);
        }
        else
        {
            _projectStrategies[projectName].Remove(toolName);
        }
    }

    public Task<(bool success, string output)> ExecuteAsync(ToolCommand command, string? projectName = null)
    {
        // Projekt-spezifische Strategie?
        if (projectName is not null &&
            _projectStrategies.TryGetValue(projectName, out var strategiesForProject) &&
            strategiesForProject.TryGetValue(command.ToolName, out var projectStrategy))
        {
            return projectStrategy.ExecuteAsync(command);
        }

        // Fallback auf global
        if (_globalStrategies.TryGetValue(command.ToolName, out var globalStrategy))
        {
            return globalStrategy.ExecuteAsync(command);
        }

        throw new InvalidOperationException(
            $"No execution strategy registered for tool '{command.ToolName}'" +
            (projectName is not null ? $" in project '{projectName}'" : string.Empty));
    }
}