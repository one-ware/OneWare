using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Core.ModuleLogic;

public sealed class OneWareCliModuleManager
{
    private readonly OneWareCliModuleCatalog _catalog;
    private ILogger? _logger;

    public OneWareCliModuleManager(OneWareCliModuleCatalog catalog)
    {
        _catalog = catalog;
    }

    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void RegisterModuleServices(IServiceCollection services, IEnumerable<IOneWareCliModule>? modules = null)
    {
        foreach (var module in GetInitializationOrder(modules))
            try
            {
                module.RegisterServices(services);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Registering services for module '{module.Id}' failed: {ex.Message}", ex);
            }
    }

    public IReadOnlyList<Command> RegisterCommands(IServiceProvider provider,
        IEnumerable<IOneWareCliModule>? modules = null)
    {
        List<Command> commands = new();
        HashSet<string> registeredNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (var module in GetInitializationOrder(modules))
            try
            {
                foreach (var command in module.RegisterCommands(provider))
                {
                    if (!TryRegisterCommandName(module, command, registeredNames))
                        continue;

                    commands.Add(command);
                }

                _logger?.Log($"CLI commands for module '{module.Id}' registered.");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Registering commands for module '{module.Id}' failed: {ex.Message}", ex);
            }

        return commands;
    }

    public IReadOnlyList<IOneWareCliModule> GetInitializationOrder(IEnumerable<IOneWareCliModule>? modules = null)
    {
        var moduleList = (modules ?? _catalog.Modules).ToList();
        if (moduleList.Count <= 1)
            return moduleList;

        var moduleById = moduleList
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var catalogById = _catalog.Modules
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var edges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in moduleList) indegree.TryAdd(module.Id, 0);

        foreach (var module in moduleList)
        foreach (var dependency in module.Dependencies ?? [])
        {
            if (!moduleById.ContainsKey(dependency))
            {
                if (!catalogById.ContainsKey(dependency))
                    _logger?.Warning($"Module '{module.Id}' depends on missing module '{dependency}'.");
                continue;
            }

            if (!edges.TryGetValue(dependency, out var list))
            {
                list = new List<string>();
                edges[dependency] = list;
            }

            list.Add(module.Id);
            indegree[module.Id] = indegree.GetValueOrDefault(module.Id) + 1;
        }

        var queue = new Queue<string>(moduleList.Where(m => indegree[m.Id] == 0).Select(m => m.Id));
        var ordered = new List<IOneWareCliModule>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            ordered.Add(moduleById[id]);

            if (!edges.TryGetValue(id, out var dependents))
                continue;

            foreach (var dependent in dependents)
            {
                indegree[dependent]--;
                if (indegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if (ordered.Count != moduleList.Count)
        {
            _logger?.Warning("Module dependency graph contains cycles; falling back to declared order.");
            return moduleList;
        }

        return ordered;
    }

    private bool TryRegisterCommandName(IOneWareCliModule module, Command command, HashSet<string> registeredNames)
    {
        if (!registeredNames.Add(command.Name))
        {
            _logger?.LogError("Skipping CLI command '{CommandName}' from module '{ModuleId}' because the name is already registered.",
                command.Name, module.Id);
            return false;
        }

        foreach (var alias in command.Aliases)
            if (!registeredNames.Add(alias))
            {
                _logger?.LogError(
                    "Skipping CLI command '{CommandName}' from module '{ModuleId}' because the alias '{Alias}' is already registered.",
                    command.Name, module.Id, alias);
                registeredNames.Remove(command.Name);
                return false;
            }

        return true;
    }
}