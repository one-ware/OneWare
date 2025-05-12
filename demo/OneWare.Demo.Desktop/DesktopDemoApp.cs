using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
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

namespace OneWare.Demo.Desktop;

public class DesktopDemoApp : DemoApp
{
    public override void Initialize()
    {
        base.Initialize();

        // Initialize plugin modules
        // Register Autofac modules directly in Initialize
        var builder = new ContainerBuilder();

        // Register your modules
        builder.RegisterModule<PackageManagerModule>();
        builder.RegisterModule<TerminalManagerModule>();
        builder.RegisterModule<SourceControlModule>();


        // Optional plugin loading from plugin directory
        try
        {
            var plugins = Directory.GetDirectories(Paths.PluginsDirectory);
            foreach (var module in plugins)
                Container.Resolve<IPluginService>().AddPlugin(module);
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }

        // Check --modules argument
        var args = Environment.GetCommandLineArgs();
        var m = Array.IndexOf(args, "--modules");
        if (m >= 0 && m < args.Length - 1)
        {
            var pluginPath = args[m + 1];
            Container.Resolve<IPluginService>().AddPlugin(pluginPath);
        }
    }

    protected override async Task LoadContentAsync()
    {
        var args = Environment.GetCommandLineArgs();
        Window? splashWindow = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            splashWindow = new SplashWindow
            {
                DataContext = Container.Resolve<SplashWindowViewModel>()
            };
            splashWindow.Show();
        }

        if (args.Length > 1 && !args[1].StartsWith("--") && File.Exists(args[1]))
        {
            var file = Container.Resolve<IProjectExplorerService>().GetTemporaryFile(args[1]);
            _ = Container.Resolve<IDockService>().OpenFileAsync(file);
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
            var logger = Container.Resolve<ILogger>();

            logger.Log("Loading last projects finished!", ConsoleColor.Cyan);

            if (settingsService.GetSettingValue<string>("LastVersion") != Global.VersionCode)
            {
                settingsService.SetSettingValue("LastVersion", Global.VersionCode);
                Container.Resolve<IWindowService>().ShowNotificationWithButton(
                    "Update Successful!",
                    $"{Paths.AppName} updated to {Global.VersionCode}!",
                    "View Changelog",
                    () => Container.Resolve<IWindowService>().Show(new ChangelogView()),
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage
                );
            }
        }
        catch (Exception e)
        {
            Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
}
