using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia; // For Avalonia-specific types like Current, AppBuilder (implicitly)
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes; // For IClassicDesktopStyleApplicationLifetime
using Avalonia.Controls.Notifications; // For ShowNotificationWithButton
using Avalonia.Media; // For IImage
using Autofac; // For Container.Resolve<T>
using Microsoft.Extensions.Logging; // For ILogger<T>
using OneWare.Core.Data; // For Global.VersionCode
using OneWare.Core.Views.Windows; // Assuming SplashWindow is here
using OneWare.Demo.Desktop.ViewModels; // Assuming SplashWindowViewModel is here
using OneWare.Demo.Desktop.Views; // Assuming ChangelogView is here
using OneWare.Essentials.Enums; // For AppState
using OneWare.Essentials.Services; // For ISettingsService, IApplicationStateService, IProjectExplorerService, IDockService, IWindowService, IPaths
using ImTools;
using OneWare.Demo.Desktop.Modules;
using OneWare.Core.ViewModels.Windows; // Make sure this is installed via NuGet if still used for .IndexOf(...)

namespace OneWare.Demo.Desktop;

// DesktopDemoApp inherits from DemoApp, which is your main Avalonia.Application class.
public class DesktopDemoApp : DemoApp
{
    // This method is part of the Avalonia application lifecycle.
    // It is called after the Avalonia UI framework has initialized,
    // and after the base DemoApp's OnFrameworkInitializationCompleted has run.
    public override void OnFrameworkInitializationCompleted()
    {


        // 1. Call base first to build the core container in DemoApp (from the core assembly).
        // At this point, DemoApp.Container will be initialized with all core services.
        base.OnFrameworkInitializationCompleted();

        // 2. Register desktop-specific modules into the existing container.
        // Create a new ContainerBuilder to hold desktop-specific registrations.
        var desktopBuilder = new ContainerBuilder();

        // Register your UiModule here. This will add all your UI-related ViewModels and Views
        // (like SplashWindowViewModel) to the container.
        desktopBuilder.RegisterModule<UiModule>();

        // Add other desktop-specific modules that live in this assembly or are conceptually part of the desktop layer.
        // For example, if your ManagerModule is actually in the Desktop assembly:
        // desktopBuilder.RegisterModule<ManagerModule>();
        // desktopBuilder.RegisterModule<PackageManagerAutofacModule>();
        // desktopBuilder.RegisterModule<TerminalManagerAutofacModule>();
        // desktopBuilder.RegisterModule<SourceControlAutofacModule>();

        // IMPORTANT: Use the Update method to add these new registrations to the already built static container
        // from the base (core) DemoApp.
        // Use '!' as Container is guaranteed non-null after base call.


        // 2. Start the asynchronous content loading process.
        // We use '_' to discard the Task returned by LoadContentAsync().
        // This makes LoadContentAsync run in the background without blocking the UI thread
        // at this point. It allows your splash screen to appear quickly while content loads.
        // If you needed to block and wait for content to load before ANY UI appeared,
        // you would make this method 'async Task' and 'await LoadContentAsync();'
        try
        {
            

            _ = LoadContentAsync();

        }
        catch (Exception)
        {

            throw;
        }

    }

    // This method handles the asynchronous loading of application content,
    // such as showing a splash screen, loading projects, and checking for updates.
    protected override async Task LoadContentAsync()
    {
        // Resolve an ILogger instance specifically for DesktopDemoApp from the container.
        // The '!' (null-forgiving operator) indicates we're confident Container is not null
        // because OnFrameworkInitializationCompleted has already ensured it.
        var logger = Container!.Resolve<ILogger<DesktopDemoApp>>();

        var arguments = Environment.GetCommandLineArgs();

        Window? splashWindow = null;
        // Check if running in a classic desktop environment to show the splash screen.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            //var content = Container!.Resolve<SplashWindowViewModel>();
            // Create and show the splash window, resolving its ViewModel from the container.
            splashWindow = new SplashWindow
            {
                DataContext = new SplashWindowViewModel()
            };
            splashWindow.Show();
        }

        // Logic to open files passed as command-line arguments.
        if (arguments.Length > 1 && !arguments[1].StartsWith("--"))
        {
            var fileName = arguments[1];
            if (File.Exists(fileName))
            {
                if (Path.GetExtension(fileName).StartsWith(".", StringComparison.OrdinalIgnoreCase))
                {
                    // Resolve necessary services and open the file.
                    var file = Container!.Resolve<IProjectExplorerService>().GetTemporaryFile(fileName);
                    _ = Container!.Resolve<IDockService>().OpenFileAsync(file);
                }
                else
                {
                    logger.LogWarning("Could not load file {FileName} due to invalid extension.", fileName);
                }
            }
            else
            {
                logger.LogWarning("Could not load file {FileName} as it does not exist.", fileName);
            }
        }
        else // If no file argument, load the last opened projects.
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                // Resolve services for managing application state and projects.
                var applicationStateService = Container!.Resolve<IApplicationStateService>();
                var projectExplorerService = Container!.Resolve<IProjectExplorerService>();
                var dockService = Container!.Resolve<IDockService>();

                // Update application state during loading.
                var key = applicationStateService.AddState("Loading last projects...", AppState.Loading);
                await projectExplorerService.OpenLastProjectsFileAsync(); // Asynchronously open last projects.
                dockService.InitializeContent(); // Initialize docking content (e.g., layout).
                applicationStateService.RemoveState(key, "Projects loaded!"); // Update state upon completion.
            }
        }

        // Simulate a minimum loading time for the splash screen to be visible.
        await Task.Delay(1000);
        splashWindow?.Close(); // Close the splash window after content loading.

        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
              //  var mainWindow = Container!.Resolve<OneWare.Core.Views.Windows.MainWindow>();
                var mainWindowViewModel = Container!.Resolve<MainWindowViewModel>();
                // Crucial: Set IsVisible to false initially.
                // It will be made visible in LoadContentAsync after DataContext is set.                
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainWindowViewModel
                };

                desktop.MainWindow.Show();
            }

          

            //if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime && desktopLifetime.MainWindow != null)
            //{
            //    // Set the main window's DataContext now that loading is complete.
            //    desktopLifetime.MainWindow.DataContext = Container!.Resolve<MainWindowViewModel>();

            //    // Crucial: Make the main window visible only after its DataContext is set.
            //    desktopLifetime.MainWindow.IsVisible = true;
            //}

            var settingsService = Container!.Resolve<ISettingsService>();
            logger.LogInformation("Loading last projects finished!");

            if (settingsService.GetSettingValue<string>("LastVersion") != Global.VersionCode)
            {
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);

                var windowService = Container!.Resolve<IWindowService>();
                var pathsService = Container!.Resolve<IPaths>();

                windowService.ShowNotificationWithButton("Update Successful!",
                    $"{pathsService.AppName} got updated to {Global.VersionCode}!", "View Changelog",
                    () => { windowService.Show(Container!.Resolve<ChangelogView>()); },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as Avalonia.Media.IImage);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred during post-content loading operations.");
        }
    
    }
}
