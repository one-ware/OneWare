using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Core.ModuleLogic;

public sealed class OneWareModuleManager
{
    private readonly OneWareModuleCatalog _catalog;
    private readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);
    private ILogger? _logger;

    public OneWareModuleManager(OneWareModuleCatalog catalog)
    {
        _catalog = catalog;
    }

    public bool InitializationCompleted { get; private set; }

    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void RegisterModuleServices(IServiceCollection services, IEnumerable<IOneWareModule>? modules = null)
    {
        foreach (var module in GetInitializationOrder(modules))
            try
            {
                module.RegisterServices(services);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Registering services for module '{module.Id}' failed: {ex.Message}", ex);
            }
    }

    public void InitializeModules(IServiceProvider provider, IEnumerable<IOneWareModule>? modules = null)
    {
        var initializingAll = modules == null;
        foreach (var module in GetInitializationOrder(modules))
        {
            if (_initialized.Contains(module.Id))
                continue;

            try
            {
                module.Initialize(provider);
                _initialized.Add(module.Id);
                _logger?.Log($"Module '{module.Id}' initialized.");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Initializing module '{module.Id}' failed: {ex.Message}", ex);
            }
        }

        if (initializingAll)
            InitializationCompleted = true;
    }

    public IReadOnlyList<IOneWareModule> GetInitializationOrder(IEnumerable<IOneWareModule>? modules = null)
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
        var ordered = new List<IOneWareModule>();

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
}