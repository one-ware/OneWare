using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Autofac;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
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
using OneWare.Studio.Desktop.Views;
using OneWare.Studio.Desktop.ViewModels;
using OneWare.TerminalManager;
using OneWare.Updater;
using OneWare.Updater.ViewModels;
using OneWare.Updater.Views;
using OneWare.Verilog;
using OneWare.Vhdl;
using OneWare.Core.Services;
using OneWare.PackageManager.Services;
using OneWare.ProjectSystem.Services;
using OneWare.Settings;
using OneWare.ErrorList;

namespace OneWare.Studio.Desktop;

public class DesktopStudioApp : StudioApp
{
    private Window? _splashWindow;
    protected override string GetDefaultLayoutName => "Desktop";
    public static IContainer Container { get; private set; } = null!;

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureContainer();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

    private void ConfigureContainer()
    {
        var builder = new ContainerBuilder();

        // Pre-register core services
        builder.RegisterType<Logger>().As<ILogger>().SingleInstance();
        builder.RegisterType<SettingsService>().As<ISettingsService>().SingleInstance();
        builder.RegisterType<WindowService>().As<IWindowService>().SingleInstance();
        builder.RegisterType<DockService>().As<IDockService>().SingleInstance();
        builder.RegisterType<ApplicationStateService>().As<IApplicationStateService>().SingleInstance();
        builder.RegisterType<ProjectManagerService>().As<IProjectManagerService>().SingleInstance();
        builder.RegisterType<PluginService>().As<IPluginService>().SingleInstance();
        builder.RegisterType<PackageService>().As<IPackageService>().SingleInstance();


        // Register Paths with constructor parameters
        builder.RegisterType<Paths>()
            .As<IPaths>()
            .WithParameter("appName", "OneWare Studio")
            .WithParameter("appIconPath", "Assets/Icons/studio.ico")
            .SingleInstance();

        // Register views and viewmodels
        builder.RegisterType<MainWindow>().SingleInstance();
        builder.RegisterType<MainWindowViewModel>().SingleInstance();
        builder.RegisterType<SplashWindow>().InstancePerDependency();
        builder.RegisterType<SplashWindowViewModel>().InstancePerDependency();
        builder.RegisterType<PackageManagerView>().InstancePerDependency();
        builder.RegisterType<PackageManagerViewModel>().InstancePerDependency();
        builder.RegisterType<UpdaterView>().InstancePerDependency();
        builder.RegisterType<UpdaterViewModel>().SingleInstance();
        builder.RegisterType<HttpService>().As<IHttpService>().SingleInstance();


        // Register modules
        builder.RegisterModule<UpdaterModule>();
        builder.RegisterModule<PackageManagerModule>();
        builder.RegisterModule<SourceControlModule>();
        builder.RegisterModule<SerialMonitorModule>();
        builder.RegisterModule<TerminalManagerModule>();
        builder.RegisterModule<CppModule>();
        builder.RegisterModule<VhdlModule>();
        builder.RegisterModule<VerilogModule>();
        builder.RegisterModule<OssCadSuiteIntegrationModule>();
        builder.RegisterModule<ErrorListModule>();


        // Build temporary container to resolve IPaths and IPluginService
        var tempContainer = builder.Build();
        var pluginService = tempContainer.Resolve<IPluginService>();
        var paths = tempContainer.Resolve<IPaths>();

        try
        {
            var args = Environment.GetCommandLineArgs();

            // Handle command-line plugins
            var m = Array.IndexOf(args, "--modules");
            if (m >= 0 && m < args.Length - 1)
            {
                var path = args[m + 1];
                pluginService.AddPlugin(path);
            }

            // Load plugins from directory
            var plugins = Directory.GetDirectories(paths.PluginsDirectory);
            foreach (var module in plugins)
            {
                pluginService.AddPlugin(module);
            }

            builder.RegisterInstance(pluginService).As<IPluginService>().SingleInstance();
        }
        catch (Exception e)
        {
            Console.WriteLine("Plugin loading failed: " + e.Message);
        }

        // Finalize container
        Container = builder.Build();
    }

    protected override async Task LoadContentAsync()
    {
        var logger = Container.Resolve<ILogger>();
        var settingsService = Container.Resolve<ISettingsService>();
        var projectExplorer = Container.Resolve<IProjectExplorerService>();
        var windowService = Container.Resolve<IWindowService>();

        try
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && !args[1].StartsWith("--"))
            {
                var fileName = args[1];
                if (File.Exists(fileName))
                {
                    _tempMode = true;
                    var dockService = Container.Resolve<IDockService>();

                    foreach (var view in dockService.SearchView<Document>().ToArray())
                    {
                        if (view is IDockable dockable)
                            dockService.CloseDockable(dockable);
                    }

                    var ext = Path.GetExtension(fileName);
                    var manager = Container.Resolve<IProjectManagerService>().GetManagerByExtension(ext);

                    if (manager != null)
                    {
                        await projectExplorer.LoadProjectAsync(fileName, manager);
                    }
                    else if (ext.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    {
                        var file = projectExplorer.GetTemporaryFile(fileName);
                        _ = Container.Resolve<IDockService>().OpenFileAsync(file);
                    }
                    else
                    {
                        logger.Warning("Could not load file/directory " + fileName);
                    }
                }
            }
            else
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
                {
                    var stateKey = Container.Resolve<IApplicationStateService>()
                        .AddState("Loading last projects...", AppState.Loading);

                    await projectExplorer.OpenLastProjectsFileAsync();
                    Container.Resolve<IDockService>().InitializeContent();

                    Container.Resolve<IApplicationStateService>().RemoveState(stateKey, "Projects loaded!");
                    logger.Log("Loading last projects finished!", ConsoleColor.Cyan);
                }
            }

            await Task.Delay(1000);
            _splashWindow?.Close();

            if (Version.TryParse(settingsService.GetSettingValue<string>("LastVersion"), out var lastVer) &&
                lastVer < Assembly.GetExecutingAssembly().GetName().Version)
            {
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);
                windowService.ShowNotificationWithButton("Update Successful!",
                    $"{Container.Resolve<IPaths>().AppName} got updated to {Global.VersionCode}!",
                    "View Changelog",
                    () =>
                    {
                        windowService.Show(new ChangelogView
                        {
                            DataContext = Container.Resolve<ChangelogViewModel>()
                        });
                    },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
            }

            var pkgService = Container.Resolve<IPackageService>();
            await pkgService.LoadPackagesAsync();

            var updates = pkgService.Packages.Values
                .Where(p => p.Status == PackageStatus.UpdateAvailable)
                .ToList();

            if (updates.Any())
            {
                windowService.ShowNotificationWithButton("Package Updates Available",
                    $"Updates for {string.Join(", ", updates.Select(x => x.Package.Name))} available!",
                    "Download", () =>
                    {
                        windowService.Show(new PackageManagerView
                        {
                            DataContext = Container.Resolve<PackageManagerViewModel>()
                        });
                    },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
            }

            var updater = Container.Resolve<UpdaterViewModel>();
            if (await updater.CheckForUpdateAsync())
            {
                Dispatcher.UIThread.Post(() =>
                {
                    windowService.ShowNotificationWithButton("Update Available",
                        $"{Container.Resolve<IPaths>().AppName} {updater.NewVersion} is available!",
                        "Download", () =>
                        {
                            windowService.Show(new UpdaterView
                            {
                                DataContext = Container.Resolve<UpdaterViewModel>()
                            });
                        },
                        Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message, ex);
        }
    }
}
