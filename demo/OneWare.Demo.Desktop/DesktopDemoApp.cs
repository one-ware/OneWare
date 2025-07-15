using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using ImTools;
using OneWare.Core.Data;
using OneWare.Core.Views.Windows;
using OneWare.Demo.Desktop.ViewModels;
using OneWare.Demo.Desktop.Views;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.PackageManager;
using OneWare.SourceControl;
using OneWare.TerminalManager;
using Prism.Ioc;
using Prism.Modularity;
using System.CommandLine;

namespace OneWare.Demo.Desktop;

public class DesktopDemoApp : DemoApp
{
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);

        moduleCatalog.AddModule<PackageManagerModule>();
        moduleCatalog.AddModule<TerminalManagerModule>();
        moduleCatalog.AddModule<SourceControlModule>();

        try
        {
            var plugins = Directory.GetDirectories(Paths.PluginsDirectory);
            foreach (var module in plugins) Container.Resolve<IPluginService>().AddPlugin(module);
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }
        
        if (Environment.GetEnvironmentVariable("MODULES") is { } pluginPath)
        {
            Container.Resolve<IPluginService>().AddPlugin(pluginPath);
        }
    }

    protected override async Task LoadContentAsync()
    {
        var arguments = Environment.GetCommandLineArgs();

        Window? splashWindow = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            splashWindow = new SplashWindow
            {
                DataContext = Container.Resolve<SplashWindowViewModel>()
            };
            splashWindow.Show();
        }

        if (arguments.Length > 1 && !arguments[1].StartsWith("--"))
        {
            var fileName = arguments[1];
            //Check file exists
            if (File.Exists(fileName))
            {
                if (Path.GetExtension(fileName).StartsWith(".", StringComparison.OrdinalIgnoreCase))
                {
                    var file = Container.Resolve<IProjectExplorerService>().GetTemporaryFile(fileName);
                    _ = Container.Resolve<IDockService>().OpenFileAsync(file);
                }
                else
                {
                    Container.Resolve<ILogger>()?.Log("Could not load file " + fileName);
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
            }
        }

        await Task.Delay(1000);
        splashWindow?.Close();

        try
        {
            var settingsService = Container.Resolve<ISettingsService>();
            Container.Resolve<ILogger>()?.Log("Loading last projects finished!", ConsoleColor.Cyan);

            if (settingsService.GetSettingValue<string>("LastVersion") != Global.VersionCode)
            {
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);

                Container.Resolve<IWindowService>().ShowNotificationWithButton("Update Successful!",
                    $"{Container.Resolve<IPaths>().AppName} got updated to {Global.VersionCode}!", "View Changelog",
                    () => { Container.Resolve<IWindowService>().Show(new ChangelogView()); },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
            }

            //await Task.Factory.StartNew(() =>
            //{
            //_ = Global.PackageManagerViewModel.CheckForUpdateAsync();
            //}, new CancellationToken(), TaskCreationOptions.None, PriorityScheduler.BelowNormal);
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}