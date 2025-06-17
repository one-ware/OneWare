using System;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using ImTools;
using Microsoft.Extensions.Logging;
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
    public readonly IPluginService _pluginService;
    public readonly ILogger<DesktopDemoApp> _logger;
    public readonly SplashWindowViewModel _splashWindowViewModel;
    public readonly IProjectExplorerService _projectExplorerService;
    public readonly IDockService _dockService ;
    public readonly IWindowService _windowService;
    public readonly IApplicationStateService _applicationStateService;
    public readonly ISettingsService _settingsService;
    public readonly IPaths _paths;

    public DesktopDemoApp(IPluginService pluginService,
                           SplashWindowViewModel splashWindowViewModel,
                           IProjectExplorerService projectExplorerService,
                           IApplicationStateService applicationStateService,
                           ISettingsService settingsService,
                           IPaths paths,
                          ILogger<DesktopDemoApp> logger)
    {
        _pluginService = pluginService;
        _logger = logger;
        _splashWindowViewModel = splashWindowViewModel;
        _projectExplorerService = projectExplorerService;
        _applicationStateService = applicationStateService;
        _settingsService = settingsService;
        _paths = paths;
    }

    

    protected void ConfigureModuleCatalog()
    {
        //base.ConfigureModuleCatalog(moduleCatalog);

        // Register built-in modules
        //builder.RegisterModule<PackageManagerModule>();
        //builder.RegisterModule<TerminalManagerModule>();
        //builder.RegisterModule<SourceControlModule>();


        try
        {
            var plugins = Directory.GetDirectories(Paths.PluginsDirectory);
            foreach (var module in plugins) _pluginService.AddPlugin(module);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
        }

        var commandLineArgs = Environment.GetCommandLineArgs();
        if (commandLineArgs.Length > 1)
        {
            var m = commandLineArgs.IndexOf(x => x == "--modules");
            if (m >= 0 && m < commandLineArgs.Length - 1)
            {
                var path = commandLineArgs[m + 1];
                _pluginService.AddPlugin(path);
            }
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
                DataContext = _splashWindowViewModel 
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
                    var file = _projectExplorerService.GetTemporaryFile(fileName);
                    _ = _dockService.OpenFileAsync(file);
                }
                else
                {
                    _logger.LogInformation("Could not load file " + fileName);
                }
            }
        }
        else
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                var key = _applicationStateService.AddState("Loading last projects...", AppState.Loading);
                await _projectExplorerService.OpenLastProjectsFileAsync();
                _dockService.InitializeContent();
                _applicationStateService.RemoveState(key, "Projects loaded!");
            }
        }

        await Task.Delay(1000);
        splashWindow?.Close();

        try
        {
            _logger.LogInformation("Loading last projects finished!", ConsoleColor.Cyan);

            if (_settingsService.GetSettingValue<string>("LastVersion") != Global.VersionCode)
            {
                _settingsService.SetSettingValue("LastVersion", Global.VersionCode);

                _windowService.ShowNotificationWithButton("Update Successful!",
                    $"{_paths.AppName} got updated to {Global.VersionCode}!", "View Changelog",
                    () => { _windowService.Show(new ChangelogView()); },
                    Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
        }
    }
}
