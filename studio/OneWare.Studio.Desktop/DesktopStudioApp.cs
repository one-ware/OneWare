using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using ImTools;
using OneWare.Core.Data;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.Cpp;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration;
using OneWare.PackageManager;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;
using OneWare.SerialMonitor;
using OneWare.SourceControl;
using OneWare.Studio.Desktop.ViewModels;
using OneWare.Studio.Desktop.Views;
using OneWare.TerminalManager;
using OneWare.Updater;
using OneWare.Updater.ViewModels;
using OneWare.Updater.Views;
using OneWare.Verilog;
using OneWare.Vhdl;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Studio.Desktop;

public class DesktopStudioApp : StudioApp
{
    private Window? _splashWindow;

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);

        moduleCatalog.AddModule<UpdaterModule>();
        moduleCatalog.AddModule<PackageManagerModule>();
        moduleCatalog.AddModule<TerminalManagerModule>();
        moduleCatalog.AddModule<SourceControlModule>();
        moduleCatalog.AddModule<SerialMonitorModule>();
        moduleCatalog.AddModule<CppModule>();
        moduleCatalog.AddModule<VhdlModule>();
        moduleCatalog.AddModule<VerilogModule>();
        moduleCatalog.AddModule<OssCadSuiteIntegrationModule>();
        
        try
        {
            if (Environment.GetEnvironmentVariable("ONEWARE_MODULES") is { } pluginPath)
            {
                Container.Resolve<IPluginService>().AddPlugin(pluginPath);
            }

            var plugins = Directory.GetDirectories(Paths.PluginsDirectory);
            foreach (var module in plugins) Container.Resolve<IPluginService>().AddPlugin(module);
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            _splashWindow = new SplashWindow
            {
                DataContext = Container.Resolve<SplashWindowViewModel>()
            };
            _splashWindow.Show();
            _splashWindow.Activate();
        }

        base.OnFrameworkInitializationCompleted();
    }

    protected override async Task LoadContentAsync()
    {
        Container.Resolve<IPackageService>().RegisterPackageRepository(
            $"https://raw.githubusercontent.com/one-ware/OneWare.PublicPackages/main/oneware-packages.json");

        var arguments = Environment.GetCommandLineArgs();

        if (arguments.Length > 1 && !arguments[1].StartsWith("--"))
        {
            var fileName = arguments[1];
            //Check file exists
            if (File.Exists(fileName))
            {
                _tempMode = true;
                var dockService = Container.Resolve<IDockService>();

                var views = dockService.SearchView<Document>();

                foreach (var view in views.ToArray())
                    if (view is IDockable dockable)
                        dockService.CloseDockable(dockable);

                var extension = Path.GetExtension(fileName);

                var manager = Container.Resolve<IProjectManagerService>().GetManagerByExtension(extension);

                if (manager != null)
                {
                    await Container.Resolve<IProjectExplorerService>().LoadProjectAsync(fileName, manager);
                }
                else if (extension.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                {
                    var file = Container.Resolve<IProjectExplorerService>().GetTemporaryFile(fileName);
                    _ = Container.Resolve<IDockService>().OpenFileAsync(file);
                }
                else
                {
                    Container.Resolve<ILogger>()?.Warning("Could not load file/directory " + fileName);
                }
            }
        }
        else
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                var key = Container.Resolve<IApplicationStateService>()
                    .AddState("Loading last projects...", AppState.Loading);
                await Container.Resolve<IProjectExplorerService>().OpenLastProjectsFileAsync();
                Container.Resolve<IDockService>().InitializeContent();
                Container.Resolve<IApplicationStateService>().RemoveState(key, "Projects loaded!");
                Container.Resolve<ILogger>()?.Log("Loading last projects finished!", ConsoleColor.Cyan);
            }
        }

        await Task.Delay(1000);
        
        _splashWindow?.Close();

        try
        {
            var settingsService = Container.Resolve<ISettingsService>();

            if (Version.TryParse(settingsService.GetSettingValue<string>("LastVersion"), out var lastVersion) &&
                lastVersion < Assembly.GetExecutingAssembly().GetName().Version)
            {
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);

                Container.Resolve<IWindowService>().ShowNotificationWithButton("Update Successful!",
                    $"{Container.Resolve<IPaths>().AppName} got updated to {Global.VersionCode}!", "View Changelog",
                    () =>
                    {
                        Container.Resolve<IWindowService>().Show(new ChangelogView
                        {
                            DataContext = Container.Resolve<ChangelogViewModel>()
                        });
                    },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
            }

            var packageService = Container.Resolve<IPackageService>();

            await packageService.LoadPackagesAsync();

            var updatePackages = packageService.Packages
                .Where(x => x.Value.Status == PackageStatus.UpdateAvailable)
                .Select(x => x.Value)
                .ToList();

            if (updatePackages.Count > 0)
                Container.Resolve<IWindowService>().ShowNotificationWithButton("Package Updates Available",
                    $"Updates for {string.Join(", ", updatePackages.Select(x => x.Package.Name))} available!",
                    "Download", () => Container.Resolve<IWindowService>().Show(new PackageManagerView
                    {
                        DataContext = Container.Resolve<PackageManagerViewModel>()
                    }),
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);

            var ideUpdater = Container.Resolve<UpdaterViewModel>();

            var canUpdate = await ideUpdater.CheckForUpdateAsync();

            if (canUpdate)
                Dispatcher.UIThread.Post(() =>
                {
                    Container.Resolve<IWindowService>().ShowNotificationWithButton("Update Available",
                        $"{Paths.AppName} {ideUpdater.NewVersion} is available!", "Download", () => Container
                            .Resolve<IWindowService>().Show(new UpdaterView
                            {
                                DataContext = Container.Resolve<UpdaterViewModel>()
                            }),
                        Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
                });
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}