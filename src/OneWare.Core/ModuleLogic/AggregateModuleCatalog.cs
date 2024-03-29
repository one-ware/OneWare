using System.Collections.ObjectModel;
using OneWare.Essentials.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Core.ModuleLogic
{
    public class AggregateModuleCatalog : IModuleCatalog
    {
        private List<IModuleCatalog> _catalogs = new List<IModuleCatalog>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateModuleCatalog"/> class
        /// </summary>
        public AggregateModuleCatalog()
        {
            this._catalogs.Add(new ModuleCatalog());
        }

        public ReadOnlyCollection<IModuleCatalog> Catalogs => this._catalogs.AsReadOnly();

        /// <summary>
        /// Adds the catalog to the list of catalogs
        /// </summary>
        /// <param name="catalog">The catalog to add</param>
        public void AddCatalog(IModuleCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentException(nameof(catalog));
            }

            this._catalogs.Add(catalog);
        }

        /// <summary>
        /// Gets all the <see cref="ModuleInfo"/> classes that are in the case <see cref="ModuleCatalog"/>
        /// </summary>
        public IEnumerable<IModuleInfo> Modules
        {
            get { return Catalogs.SelectMany(x => x.Modules); }
        }

        IEnumerable<IModuleInfo> IModuleCatalog.Modules => Modules;

        /// <summary>
        /// Returns the list of <see cref="ModuleInfo"/> that <param name="moduleInfo" /> depends on
        /// </summary>
        /// <param name="moduleInfo">The <see cref="ModuleInfo"/> to get</param>
        /// <returns>
        /// An enumeration of <see cref="ModuleInfo"/> that <paramref name="moduleInfo"/> depends on
        /// </returns>
        public IEnumerable<IModuleInfo> GetDependentModules(IModuleInfo moduleInfo)
        {
            var catalog = this._catalogs.Single(x => x.Modules.Contains(moduleInfo));
            return catalog.GetDependentModules(moduleInfo);
        }

        /// <summary>
        /// Returns the collection of <see cref="ModuleInfo"/> that contain both the <see cref="ModuleInfo"/>s in
        /// <paramref name="modules"/>, but also all the modules they depend on
        /// </summary>
        /// <param name="modules">The modules to get the dependencies for</param>
        /// <returns>
        /// A collection of <see cref="ModuleInfo"/> that contains both all <see cref="ModuleInfo"/>s in <paramref name="modules"/>
        /// and also all the <see cref="ModuleInfo"/> they depend on
        /// </returns>
        public IEnumerable<IModuleInfo> CompleteListWithDependencies(IEnumerable<IModuleInfo> modules)
        {
            var modulesGroupedByCatalog = modules.GroupBy<IModuleInfo, IModuleCatalog>(module => this._catalogs.Single(
               catalog => catalog.Modules.Contains(module)));
            return modulesGroupedByCatalog.SelectMany(x => x.Key.CompleteListWithDependencies(x));
        }

        /// <summary>
        /// Initializes the catalog, which may load and validate the modules
        /// </summary>
        public void Initialize()
        {
            this.Catalogs.ToList().ForEach((catalog) =>
            {
                try
                {
                    catalog.Initialize();
                }
                catch (Exception e)
                {
                    ContainerLocator.Current.Resolve<ILogger>().Error(e.Message, e);
                }
            });
        }

        /// <summary>
        /// Adds a <see cref="ModuleInfo"/> to the <see cref="ModuleCatalog"/>
        /// </summary>
        /// <param name="moduleInfo">The <see cref="ModuleInfo"/> to add</param>
        public IModuleCatalog AddModule(IModuleInfo moduleInfo)
        {
            return this._catalogs[0].AddModule(moduleInfo);
        }
    }
}