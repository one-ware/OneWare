using System.Runtime.InteropServices;
using Autofac; // Import Autofac
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.Configuration; // Add this for IConfiguration, ConfigurationBuilder
using Microsoft.Extensions.Logging; // For ILogger
using OneWare.ApplicationCommands.Services;
using OneWare.Core.Data;
using OneWare.Core.Modules;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;

// Assuming these are your Autofac Modules
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Services;
using OneWare.ProjectExplorer.Services;
using OneWare.ProjectExplorer.ViewModels;
using OneWare.ProjectSystem.Services;
using OneWare.Settings;
using Serilog; // For Serilog setup and Log.Logger
using Serilog.Extensions.Logging; // For AddSerilog

namespace OneWare.Demo;

public class DemoApp : Avalonia.Application // Make sure it inherits from Avalonia.Application
{
    // The Autofac container. Made static for initial bootstrapping convenience,
    // though for most resolved services, direct injection is preferred.
    public static IContainer? Container { get; private set; }

    // Paths is kept static as it's often needed very early in the application lifecycle
    // for things like logging paths before the full DI container is built or accessible everywhere.
    // If Paths had complex dependencies, it would need to be moved into the container and injected.
    public static readonly IPaths Paths = new Paths("OneWare Demo", "avares://OneWare.Demo/Assets/icon.ico");

    // This method is part of Avalonia's application lifecycle and is a good place
    // to build the Autofac container.
    public override void OnFrameworkInitializationCompleted()
    {
        // 1. Build Configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // Set base path for appsettings.json
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)         
            .Build();

        // 2. Configure Serilog from Configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration) // Read Serilog settings from appsettings.json
            .CreateLogger();

        // Ensure log directories exist as configured in appsettings.json
        // or where crash reports are stored.
        // This assumes your Serilog file sink configuration uses relative paths
        // or paths that resolve correctly with AppContext.BaseDirectory.
        if (!Directory.Exists(Paths.CrashReportsDirectory))
        {
            Directory.CreateDirectory(Paths.CrashReportsDirectory);
        }
        //if (!Directory.Exists(Paths.LogDirectory)) // Assuming you have a LogDirectory for regular logs
        //{
        //    Directory.CreateDirectory(Paths.LogDirectory);
        //}


        // 3. Build Autofac Container
        var builder = new ContainerBuilder();

        // Register configuration itself (optional, but good practice if services need it)
        builder.RegisterInstance(configuration).As<IConfiguration>().SingleInstance();

        // Register core services
        builder.RegisterType<SettingsService>().As<ISettingsService>().SingleInstance();
        // Register the static Paths instance
        builder.RegisterInstance(Paths).As<IPaths>().SingleInstance();

        // Register the Serilog logger with Microsoft.Extensions.Logging.ILoggerFactory
        // This enables ILogger<T> to be injected anywhere.
        builder.RegisterInstance<ILoggerFactory>(new SerilogLoggerFactory(Log.Logger))
               .SingleInstance();
        builder.RegisterGeneric(typeof(Logger<>))
               .As(typeof(ILogger<>))
               .SingleInstance();

        // Register the ThemeManager
        builder.RegisterType<ThemeManager>().AsSelf().SingleInstance(); // Or As<IThemeManager>() if you have an interface

        // ----------------------------------------------------
        // Register your Autofac Modules here
        // These modules encapsulate specific service registrations (like PackageManager, TerminalManager)
        // ----------------------------------------------------
        // This is where you load your ManagerModule:
        builder.RegisterModule<ManagerModule>(); // <--- This line loads the module
        

        //builder.RegisterModule<TerminalManagerAutofacModule>(); // Placeholder: ensure you create this module
        //builder.RegisterModule<SourceControlAutofacModule>();   // Placeholder: ensure you create this module

        // ----------------------------------------------------
        // Register other application-specific services or Views/ViewModels
        // that are not part of specific modules but are resolved by the container.
        // Replace with your actual concrete implementations if they exist.
        // ----------------------------------------------------
        builder.RegisterType<PluginService>().As<IPluginService>().SingleInstance();
        //builder.RegisterType<ProjectExplorerService>().As<IProjectExplorerService>().SingleInstance();
        builder.RegisterType<DockService>().As<IDockService>().SingleInstance();
        builder.RegisterType<ApplicationStateService>().As<IApplicationStateService>().SingleInstance();
        builder.RegisterType<WindowService>().As<IWindowService>().SingleInstance();

        // Example ViewModel and View registrations if they are resolved via DI        
        builder.RegisterType<ChangelogView>().AsSelf(); // Usually views are transient

        builder.RegisterType<SettingsService>().As<ISettingsService>().SingleInstance();
        builder.RegisterType<ProjectExplorerViewModel>().As<IProjectExplorerService>().SingleInstance();

        string appName = "OneWare Demo"; // Define your application name
        string appIconPath = "avares://OneWare.Demo/Assets/icon.ico"; // Define your application icon path

        builder.RegisterType<Paths>()
               .As<IPaths>() // Register it as the interface IPaths
               .WithParameter("appName", appName)
               .WithParameter("appIconPath", appIconPath)
               .SingleInstance(); // IPaths is typically a singleton

        builder.RegisterType<WelcomeScreenViewModel>().AsSelf();
        builder.RegisterType<MainDocumentDockViewModel>().AsSelf();
        builder.RegisterType<MainWindow> ().AsSelf();
        builder.RegisterType<OneWare.Core.ViewModels.Windows.MainWindowViewModel>().AsSelf();

        builder.RegisterType<ProjectManagerService>().As<IProjectManagerService>().SingleInstance();
        builder.RegisterType<FileWatchService>().As<IFileWatchService>().SingleInstance();
        builder.RegisterType<LanguageManager>().As<ILanguageManager>().SingleInstance();
        builder.RegisterType<ApplicationCommandService>().As<IApplicationCommandService>().SingleInstance();

        // Build the container
        Container = builder.Build();

        // Perform static constructor logic that now relies on resolved services
        // These services are now correctly managed by Autofac.
        var test = Container!.Resolve<IPaths>();

        var applicationStateService = Container!.Resolve<IApplicationStateService>();
        var projectExplorerService = Container!.Resolve<IProjectExplorerService>();
        var dockService = Container!.Resolve<IDockService>();


        var settingsService = Container.Resolve<ISettingsService>();
        var mainLogger = Container.Resolve<Microsoft.Extensions.Logging.ILogger<DemoApp>>();

        settingsService.Register("LastVersion", Global.VersionCode);
        settingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        settingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog",
            "Use Managed File Dialog",
            "", RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

        mainLogger.LogInformation("Application framework initialization complete.");

        base.OnFrameworkInitializationCompleted(); // Call base last if it relies on services being set up
    }

    // This method is called after OnFrameworkInitializationCompleted,
    // and is typically used for Avalonia UI setup like loading styles.
    public override void Initialize()
    {
        // Resolve ThemeManager from the container. Its dependencies (ISettingsService, IPaths)
        // will be automatically injected by Autofac.
        var themeManager = Container?.Resolve<ThemeManager>();

        // No direct instantiation of ThemeManager here anymore.

        base.Initialize(); // Call base after your own setup if necessary

        Styles.Add(new StyleInclude(new Uri("avares://OneWare.Demo"))
        {
            Source = new Uri("avares://OneWare.Demo/Styles/Theme.axaml")
        });
    }

    protected virtual Task LoadContentAsync()
    {
        // Default (empty) implementation, or add common content loading logic here
        return Task.CompletedTask;
    }
}