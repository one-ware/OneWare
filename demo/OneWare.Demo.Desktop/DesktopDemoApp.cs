using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using OneWare.Core.Data;
using OneWare.Core.Views.Windows;
using OneWare.Demo.Desktop.ViewModels;
using OneWare.Demo.Desktop.Views;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.PackageManager;
using OneWare.SourceControl;
using OneWare.TerminalManager;
using System.CommandLine;
using Microsoft.Extensions.Logging;
using OneWare.Core.ModuleLogic;

namespace OneWare.Demo.Desktop;

public class DesktopDemoApp : DemoApp
{
    protected override void ConfigureModuleCatalog(OneWareModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);

        moduleCatalog.AddModule<PackageManagerModule>();
        moduleCatalog.AddModule<TerminalManagerModule>();
        moduleCatalog.AddModule<SourceControlModule>();

        try
        {
            var plugins = Directory.GetDirectories(Paths.PluginsDirectory);
            foreach (var module in plugins) Services.Resolve<IPluginService>().AddPlugin(module);
        }
        catch (Exception e)
        {
            Services.Resolve<ILogger>().Error(e.Message, e);
        }
        
        if (Environment.GetEnvironmentVariable("MODULES") is { } pluginPath)
        {
            Services.Resolve<IPluginService>().AddPlugin(pluginPath);
        }
    }

    protected override async Task LoadContentAsync()
    {
        await base.LoadContentAsync();
        
        var arguments = Environment.GetCommandLineArgs();

        // Window? splashWindow = null;
        // if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        // {
        //     splashWindow = new SplashWindow
        //     {
        //         DataContext = Container.Resolve<SplashWindowViewModel>()
        //     };
        //     splashWindow.Show();
        // }

        if (arguments.Length > 1 && !arguments[1].StartsWith("--"))
        {
            var fileName = arguments[1];
            //Check file exists
            if (File.Exists(fileName))
            {
                if (Path.GetExtension(fileName).StartsWith(".", StringComparison.OrdinalIgnoreCase))
                {
                    var file = Services.Resolve<IProjectExplorerService>().GetTemporaryFile(fileName);
                    _ = Services.Resolve<IMainDockService>().OpenFileAsync(file);
                }
                else
                {
                    Services.Resolve<ILogger>()?.Log("Could not load file " + fileName);
                }
            }
        }
        else
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                var key = Services.Resolve<IApplicationStateService>()
                    .AddState("Loading last projects...", AppState.Loading);
                await Services.Resolve<IProjectExplorerService>().OpenLastProjectsFileAsync();
                Services.Resolve<IMainDockService>().InitializeContent();
                Services.Resolve<IApplicationStateService>().RemoveState(key, "Projects loaded!");
            }
        }

        try
        {
            var settingsService = Services.Resolve<ISettingsService>();
            Services.Resolve<ILogger>()?.Log("Loading last projects finished!", ConsoleColor.Cyan);

            if (settingsService.GetSettingValue<string>("LastVersion") != Global.VersionCode)
            {
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);

                Services.Resolve<IWindowService>().ShowNotificationWithButton("Update Successful!",
                    $"{Services.Resolve<IPaths>().AppName} got updated to {Global.VersionCode}!", "View Changelog",
                    () => { Services.Resolve<IWindowService>().Show(new ChangelogView()); },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
            }

            //await Task.Factory.StartNew(() =>
            //{
            //_ = Global.PackageManagerViewModel.CheckForUpdateAsync();
            //}, new CancellationToken(), TaskCreationOptions.None, PriorityScheduler.BelowNormal);
        }
        catch (Exception e)
        {
            Services.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}