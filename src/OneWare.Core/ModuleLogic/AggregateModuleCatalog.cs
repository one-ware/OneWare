using System;
using System.Collections.ObjectModel;
using System.Linq;
using OneWare.Essentials.Services;
using Prism.Modularity;

namespace OneWare.Core.ModuleLogic;

public class AggregateModuleCatalog : IModuleCatalog
{
    private readonly List<IModuleCatalog> _catalogs = new();
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AggregateModuleCatalog" /> class
    /// </summary>
    public AggregateModuleCatalog(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _catalogs.Add(new ModuleCatalog());
    }

    public ReadOnlyCollection<IModuleCatalog> Catalogs => _catalogs.AsReadOnly();

    /// <summary>
    ///     Gets all the <see cref="ModuleInfo" /> classes that are in the <see cref="ModuleCatalog" />
    /// </summary>
    public IEnumerable<IModuleInfo> Modules => Catalogs.SelectMany(x => x.Modules);

    /// <summary>
    ///     Returns the list of <see cref="ModuleInfo" /> that <paramref name="moduleInfo" /> depends on
    /// </summary>
    public IEnumerable<IModuleInfo> GetDependentModules(IModuleInfo moduleInfo)
    {
        var catalog = _catalogs.Single(x => x.Modules.Contains(moduleInfo));
        return catalog.GetDependentModules(moduleInfo);
    }

    /// <summary>
    ///     Returns all <paramref name="modules" /> and their dependencies
    /// </summary>
    public IEnumerable<IModuleInfo> CompleteListWithDependencies(IEnumerable<IModuleInfo> modules)
    {
        var modulesGroupedByCatalog = modules.GroupBy(
            module => _catalogs.Single(catalog => catalog.Modules.Contains(module)));

        return modulesGroupedByCatalog.SelectMany(g => g.Key.CompleteListWithDependencies(g));
    }

    /// <summary>
    ///     Initializes all underlying catalogs
    /// </summary>
    public void Initialize()
    {
        foreach (var catalog in Catalogs)
        {
            try
            {
                catalog.Initialize();
            }
            catch (Exception e)
            {
                _logger.Error($"Failed to initialize module catalog: {e.Message}", e);
            }
        }
    }

    /// <summary>
    ///     Adds a module to the default catalog
    /// </summary>
    public IModuleCatalog AddModule(IModuleInfo moduleInfo)
    {
        return _catalogs[0].AddModule(moduleInfo);
    }

    /// <summary>
    ///     Adds a catalog to the aggregate
    /// </summary>
    public void AddCatalog(IModuleCatalog catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        _catalogs.Add(catalog);
    }
}
