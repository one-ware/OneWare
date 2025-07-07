using Microsoft.Extensions.Configuration;
using OneWare.ApplicationCommands.Services;
using OneWare.CloudIntegration.Modules;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.Debugger.Modules;
using OneWare.ErrorList.Modules;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Services;
using OneWare.FolderProjectSystem;
using OneWare.FolderProjectSystem.Modules;
using OneWare.ImageViewer.Modules;
using OneWare.Json.Modules;
using OneWare.LibraryExplorer.Modules;
using OneWare.Output.Modules;
using OneWare.ProjectExplorer.Modules;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectSystem.Services;
using OneWare.SearchList.Modules;
using OneWare.Settings;
using OneWare.Toml.Modules;
using Prism.Modularity;
using Serilog;
using Serilog.Extensions.Autofac.DependencyInjection;
using ILogger = Serilog.ILogger;

namespace OneWare.Core.Modules
{
    public class OneWareCoreModule : IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter;
        private IConfiguration _configuration;

        public OneWareCoreModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void OnExecute()
        {

        }

        public void RegisterTypes()
        {
            _configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory()) // Set base path to application's executable directory
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // Load appsettings.json
               .Build();

            var loggerConfig = new LoggerConfiguration()
               .ReadFrom.Configuration(_configuration); // READ FROM CONFIG

            // Configure Serilog
            //var loggerConfig = new LoggerConfiguration()
            //    .MinimumLevel.Debug()
            //    .WriteTo.Console();


            if (_containerAdapter is AutofacContainerAdapter autofacAdapter)
            {
                autofacAdapter.ConfigureBuilder(builder =>
                {
                    builder.RegisterSerilog(loggerConfig);
                });
            }
            else
            {
                var logger = loggerConfig.CreateLogger();
                _containerAdapter.RegisterInstance<ILogger>(logger); // Register as instance, not explicit singleton param
            }

            // RegisterInstance<IModuleCatalog>(ModuleCatalog)
            _containerAdapter.Register<IModuleCatalog, ModuleCatalog>();
            // _containerAdapter.RegisterInstance<IModuleCatalog>(ModuleCatalog);

            // Services (RegisterSingleton<IService, Service>())
            _containerAdapter.Register<IPluginService, PluginService>(isSingleton: true);
            _containerAdapter.Register<IHttpService, HttpService>(isSingleton: true);
            _containerAdapter.Register<IApplicationCommandService, ApplicationCommandService>(isSingleton: true);
            _containerAdapter.Register<IProjectManagerService, ProjectManagerService>(isSingleton: true);
            _containerAdapter.Register<ILanguageManager, LanguageManager>(isSingleton: true);
            _containerAdapter.Register<IApplicationStateService, ApplicationStateService>(isSingleton: true);
            _containerAdapter.Register<IDockService, DockService>(isSingleton: true);
            _containerAdapter.Register<IWindowService, WindowService>(isSingleton: true);
            _containerAdapter.Register<IModuleTracker, ModuleTracker>(isSingleton: true);
            _containerAdapter.Register<IPaths, Paths>(isSingleton: true);

            // Services (RegisterSingleton<IService, Service>())
            _containerAdapter.Register<IPluginService, PluginService>(isSingleton: true);
            _containerAdapter.Register<IHttpService, HttpService>(isSingleton: true);
            _containerAdapter.Register<IApplicationCommandService, ApplicationCommandService>(isSingleton: true);
            _containerAdapter.Register<IProjectManagerService, ProjectManagerService>(isSingleton: true);
            _containerAdapter.Register<ILanguageManager, LanguageManager>(isSingleton: true);
            _containerAdapter.Register<IApplicationStateService, ApplicationStateService>(isSingleton: true);
            _containerAdapter.Register<IDockService, DockService>(isSingleton: true);
            _containerAdapter.Register<IWindowService, WindowService>(isSingleton: true);
            _containerAdapter.Register<IModuleTracker, ModuleTracker>(isSingleton: true);
            _containerAdapter.Register<ISettingsService, SettingsService>(isSingleton: true);
            _containerAdapter.Register<IProjectExplorerService, ProjectExplorerViewModel>(isSingleton: true);
            _containerAdapter.Register<IFileWatchService, FileWatchService>(isSingleton: true);

            // For self-registered singletons (Service, not IService, Service)
            _containerAdapter.Register<BackupService, BackupService>(isSingleton: true);
            _containerAdapter.Register<IChildProcessService, ChildProcessService>(isSingleton: true);
            _containerAdapter.Register<IFileIconService, FileIconService>(isSingleton: true);
            _containerAdapter.Register<IEnvironmentService, EnvironmentService>(isSingleton: true);
            _containerAdapter.Register<FolderProjectManager, FolderProjectManager>(isSingleton: true);

            // ViewModels - Singletons (Self-registered)
            _containerAdapter.Register<MainWindowViewModel, MainWindowViewModel>(isSingleton: true);
            _containerAdapter.Register<MainDocumentDockViewModel, MainDocumentDockViewModel>(isSingleton: true);

            // ViewModels Transients (Self-registered)
            _containerAdapter.Register<WelcomeScreenViewModel, WelcomeScreenViewModel>(); // isSingleton defaults to false
            _containerAdapter.Register<EditViewModel, EditViewModel>();
            _containerAdapter.Register<ChangelogViewModel, ChangelogViewModel>();
            _containerAdapter.Register<AboutViewModel, AboutViewModel>();

            // Windows (Self-registered Singletons)
            _containerAdapter.Register<MainWindow, MainWindow>(isSingleton: true);
            _containerAdapter.Register<MainView, MainView>(isSingleton: true);

            RegisterModuleCatalog();
        }

        private void RegisterModuleCatalog()
        {

            new SearchListModule(_containerAdapter).RegisterTypes();
            new ErrorListModule(_containerAdapter).RegisterTypes();
            new OutputModule(_containerAdapter).RegisterTypes();
       //     new ProjectExplorerModule(_containerAdapter).RegisterTypes();
            new LibraryExplorerModule(_containerAdapter).RegisterTypes();
            //new FolderProjectSystem.Modules.FolderProjectSystemModule(_containerAdapter).RegisterTypes();
            new ImageViewerModule(_containerAdapter).RegisterTypes();
            //new JsonModule(_containerAdapter).RegisterTypes();
            //new TomlModule(_containerAdapter).RegisterTypes();
            new DebuggerModule(_containerAdapter).RegisterTypes();
            new OneWareCloudIntegrationModule(_containerAdapter).RegisterTypes();
        }
    }
}