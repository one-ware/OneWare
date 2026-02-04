using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Core.Data;
using OneWare.Core.ModuleLogic;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.Cpp;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration;
using OneWare.PackageManager;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;
using OneWare.Python;
using OneWare.SerialMonitor;
using OneWare.SourceControl;
using OneWare.Studio.Desktop.ViewModels;
using OneWare.Studio.Desktop.Views;
using OneWare.TerminalManager;
using OneWare.ToolEngine;
using OneWare.Updater;
using OneWare.Updater.ViewModels;
using OneWare.Updater.Views;
using OneWare.Verilog;
using OneWare.Vhdl;

namespace OneWare.Studio.Desktop;

public class DesktopStudioApp : StudioApp
{
    protected override void ConfigureModuleCatalog(OneWareModuleCatalog moduleCatalog)
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
        moduleCatalog.AddModule<ToolEngineModule>();
        moduleCatalog.AddModule<OssCadSuiteIntegrationModule>();
        moduleCatalog.AddModule<PythonModule>();
    }

    protected override void LoadStartupPlugins()
    {
        try
        {
            if (Environment.GetEnvironmentVariable("ONEWARE_MODULES") is { } pluginPath)
                Services.Resolve<IPluginService>().AddPlugin(pluginPath);

            var plugins = Directory.GetDirectories(Paths.PluginsDirectory);
            foreach (var module in plugins) Services.Resolve<IPluginService>().AddPlugin(module);
        }
        catch (Exception e)
        {
            Services.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        Services.Resolve<IApplicationStateService>().RegisterPathLaunchAction(x => _ = PathOpenTaskAsync(x));
        Services.Resolve<IApplicationStateService>().RegisterShutdownAction(Program.ReleaseLock);
    }

    private async Task PathOpenTaskAsync(string? path)
    {
        var fileName = path;
        //Check file exists
        if (File.Exists(fileName))
        {
            var dockService = Services.Resolve<IMainDockService>();

            var views = dockService.SearchView<Document>();

            foreach (var view in views.ToArray())
                if (view is IDockable dockable)
                    dockService.CloseDockable(dockable);

            var extension = Path.GetExtension(fileName);

            var manager = Services.Resolve<IProjectManagerService>().GetManagerByExtension(extension);

            if (manager != null)
            {
                await Services.Resolve<IProjectExplorerService>().LoadProjectAsync(fileName, manager);
            }
            else if (extension.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                var file = Services.Resolve<IProjectExplorerService>().GetTemporaryFile(fileName);
                _ = Services.Resolve<IMainDockService>().OpenFileAsync(file);
            }
            else
            {
                Services.Resolve<ILogger>()?.Warning("Could not load file/directory " + fileName);
            }
        }
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);

        services.AddSingleton<AiReleaseWindowViewModel>();
    }

    protected override AvaloniaObject CreateShell()
    {
        var shell = base.CreateShell();

        Services.Resolve<ISettingsService>().Register(AiReleaseWindowViewModel.ShowReleaseNotificationKey, true);

        return shell;
    }

    protected override async Task LoadContentAsync()
    {
        Services.Resolve<IPackageService>().RegisterPackageRepository(
            "https://raw.githubusercontent.com/one-ware/OneWare.PublicPackages/main/oneware-packages.json");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            var key = Services.Resolve<IApplicationStateService>()
                .AddState("Loading last projects...", AppState.Loading);
            await Services.Resolve<IProjectExplorerService>().OpenLastProjectsFileAsync();
            Services.Resolve<IMainDockService>().InitializeContent();
            Services.Resolve<IApplicationStateService>().RemoveState(key, "Projects loaded!");
            Services.Resolve<ILogger>()?.Log("Loading last projects finished");

            if (Environment.GetEnvironmentVariable("ONEWARE_OPEN_PATH") is { } pathOpen)
                Services.Resolve<IApplicationStateService>().ExecutePathLaunchActions(pathOpen);
        }

        // trigger AutoLaunch Actions
        await base.LoadContentAsync();

        var settingsService = Services.Resolve<ISettingsService>();
        var packageService = Services.Resolve<IPackageService>();
        var ideUpdater = Services.Resolve<UpdaterViewModel>();

        var versionGotUpdated = false;
        List<IPackageState>? updatePackages = null;
        var canUpdate = false;
        var showOneWareAiNotification = false;

        try
        {
            //step 1: IDE got updated
            //check if the current version is newer than the previous
            if (Version.TryParse(settingsService.GetSettingValue<string>("LastVersion"), out var lastVersion) &&
                lastVersion < Assembly.GetExecutingAssembly().GetName().Version)
            {
                //update the version in settings
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);
                versionGotUpdated = true;
            }

            //step 2: Load the installed plugins
            await packageService.RefreshAsync();

            //step 3: Get dated plugins
            updatePackages = packageService.Packages
                .Where(x => x.Value.Status == PackageStatus.UpdateAvailable)
                .Select(x => x.Value)
                .ToList();

            //step 4: Check if there is any IDE update
            canUpdate = await ideUpdater.CheckForUpdateAsync();

            //step 5: Check if the OneWare.AI notification should be shown
            //the setting refer to the dialog option "Don't show this again"
            showOneWareAiNotification =
                SettingsService.GetSettingValue<bool>(AiReleaseWindowViewModel.ShowReleaseNotificationKey);
        }
        catch (Exception e)
        {
            Services.Resolve<ILogger>().Error(e.Message, e);
        }

        try
        {
            //step 1: IDE got updated
            if (versionGotUpdated)
                Services.Resolve<IWindowService>().ShowNotificationWithButton("Update Successful!",
                    $"{Services.Resolve<IPaths>().AppName} got updated to {Global.VersionCode}!", "View Changelog",
                    () =>
                    {
                        Services.Resolve<IWindowService>().Show(new ChangelogView
                        {
                            DataContext = Services.Resolve<ChangelogViewModel>()
                        });
                    },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);

            //step 2: Ask to update the outdated plugins
            if (updatePackages?.Count > 0)
                Services.Resolve<IWindowService>().ShowNotificationWithButton("Package Updates Available",
                    $"Updates for {string.Join(", ", updatePackages.Select(x => x.Package.Name))} available!",
                    "Download", () => Services.Resolve<IWindowService>().Show(new PackageManagerView
                    {
                        DataContext = Services.Resolve<PackageManagerViewModel>()
                    }),
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);

            //step 3: Ask to update the IDE
            if (canUpdate)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Services.Resolve<IWindowService>().ShowNotificationWithButton("Update Available",
                        $"{Paths.AppName} {ideUpdater.NewVersion} is available!", "Download", () => Services
                            .Resolve<IWindowService>().Show(new UpdaterView
                            {
                                DataContext = Services.Resolve<UpdaterViewModel>()
                            }),
                        Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
                });
            }
            //step 4: Ask to install the OneWare.AI extension
            else if (showOneWareAiNotification && Environment.GetEnvironmentVariable("ONEWARE_OPEN_URL") == null &&
                     Environment.GetEnvironmentVariable("ONEWARE_AUTOLAUNCH") == null)
            {
                var aiReleaseWindowVm = Services.Resolve<AiReleaseWindowViewModel>();
                //check if the specified extension is already installed
                if (aiReleaseWindowVm.ExtensionIsAlreadyInstalled(Services.Resolve<IPluginService>()))
                    return;

                //if not, notify the user that the OneWare.AI extension is available
                Dispatcher.UIThread.Post(() =>
                {
                    Services.Resolve<IWindowService>().Show(new AiReleaseWindow
                    {
                        DataContext = aiReleaseWindowVm
                    });
                });
            }
        }
        catch (Exception e)
        {
            Services.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}