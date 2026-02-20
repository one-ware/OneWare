using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.ApplicationCommands.Services;
using OneWare.Core.Data;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Extensions;
using OneWare.Core.Views.Windows;
using OneWare.ErrorList;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem;
using OneWare.ImageViewer;
using OneWare.Json;
using OneWare.LibraryExplorer;
using OneWare.Output;
using OneWare.ProjectExplorer;
using OneWare.ProjectSystem.Services;
using OneWare.SearchList;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using OneWare.Toml;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using TextMateSharp.Grammars;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LoggerExtensions = OneWare.Essentials.Services.LoggerExtensions;

namespace OneWare.Core;

public class App : Application
{
    private OneWareModuleManager? _moduleManager;
    private ModuleServiceRegistry? _moduleServiceRegistry;
    protected OneWareModuleCatalog ModuleCatalog { get; } = new();

    protected virtual string GetDefaultLayoutName => "Default";

    protected IServiceProvider Services => ContainerLocator.Current;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        Animation.RegisterCustomAnimator<string, CustomStringAnimator>();
    }

    protected virtual void RegisterServices(IServiceCollection services)
    {
        services.AddLogging(ConfigureLogging);
        services.AddSingleton<ILogger>(provider =>
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("OneWare"));

        //Services
        services.AddSingleton<IPluginService, PluginService>();
        services.AddSingleton<OnnxRuntimeBootstrapper>();
        services.AddSingleton<IOnnxRuntimeService, OnnxRuntimeService>();
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<IApplicationCommandService, ApplicationCommandService>();
        services.AddSingleton<IProjectManagerService, ProjectManagerService>();
        services.AddSingleton<ILanguageManager, LanguageManager>();
        services.AddSingleton<IApplicationStateService, ApplicationStateService>();
        services.AddSingleton<IMainDockService, MainDockService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IChildProcessService, ChildProcessService>();
        services.AddSingleton<IFileIconService, FileIconService>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<IWelcomeScreenService>(provider =>
        {
            var service = new WelcomeScreenService();
            service.RegisterReceiver(provider.Resolve<WelcomeScreenViewModel>());

            return service;
        });

        //ViewModels - Singletons
        services.AddSingleton<WelcomeScreenViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainDocumentDockViewModel>();

        //ViewModels Transients
        services.AddTransient<EditViewModel>();
        services.AddTransient<ChangelogViewModel>();
        services.AddTransient<AboutViewModel>();

        //Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainSingleView>();
    }

    protected virtual AvaloniaObject CreateShell()
    {
        //Register IDE Settings
        var settingsService = Services.Resolve<ISettingsService>();
        var paths = Services.Resolve<IPaths>();

        Name = paths.AppName;

        NativeMenu.SetMenu(this, new NativeMenu
        {
            Items =
            {
                new NativeMenuItem
                {
                    Header = $"About {Name}",
                    Command = new RelayCommand(() => Services.Resolve<IWindowService>().Show(
                        new AboutView
                        {
                            DataContext = Services.Resolve<AboutViewModel>()
                        }))
                },
                new NativeMenuItemSeparator(),
                new NativeMenuItem
                {
                    Header = "Settings",
                    Command = new AsyncRelayCommand(() => Services.Resolve<IWindowService>().ShowDialogAsync(
                        new ApplicationSettingsView
                        {
                            DataContext = Services.Resolve<ApplicationSettingsViewModel>()
                        }))
                }
            }
        });

        //General
        settingsService.RegisterSettingCategory("General", 0, "Material.ToggleSwitchOutline");
        settingsService.RegisterSettingCategory("AI", 0, "AI_Img");

        //Editor settings
        settingsService.RegisterSettingCategory("Editor", 0, "BoxIcons.RegularCode");

        settingsService.RegisterSettingCategory("Tools", 0, "FeatherIcons.Tool");

        settingsService.RegisterSettingCategory("Languages", 0, "FluentIcons.ProofreadLanguageRegular");
        
        settingsService.RegisterSetting("Tools", "ONNX Runtime", OnnxRuntimeBootstrapper.SettingSelectedRuntimeKey,
            new ComboBoxSetting("Preferred Runtime (restart required)", "cpu", GetOnnxRuntimeOptions(paths))
            {
                HoverDescription =
                    "Select the runtime folder in Packages/OnnxRuntimes. Use 'auto' for OS defaults."
            });
        settingsService.RegisterSetting("AI", "ONNX Runtime", OnnxRuntimeBootstrapper.SettingRuntimePathKey,
            new TextBoxSetting("Custom Runtime Path (restart required)", string.Empty, null)
            {
                HoverDescription =
                    "Optional absolute path that overrides the runtime selection when set."
            });

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_FontFamily",
            new ComboBoxSetting("Editor Font Family", "JetBrains Mono NL",
                ["JetBrains Mono NL", "IntelOne Mono", "Consolas", "Comic Sans MS", "Fira Code"]));

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_FontSize",
            new ComboBoxSetting("Font Size", 15, Enumerable.Range(10, 30).Cast<object>().ToArray()));

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_SyntaxTheme_Dark",
            new ComboBoxSetting("Editor Theme Dark", ThemeName.DarkPlus,
                Enum.GetValues<ThemeName>().Cast<object>().ToArray())
            {
                HoverDescription = "Sets the theme for Syntax Highlighting in Dark Mode"
            });

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_SyntaxTheme_Light",
            new ComboBoxSetting("Editor Theme Light", ThemeName.LightPlus,
                Enum.GetValues<ThemeName>().Cast<object>().ToArray())
            {
                HoverDescription = "Sets the theme for Syntax Highlighting in Light Mode"
            });

        settingsService.RegisterSetting("Editor", "Formatting", "Editor_UseAutoFormatting",
            new CheckBoxSetting("Use Auto Formatting", true));

        settingsService.RegisterSetting("Editor", "Formatting", "Editor_UseAutoBracket",
            new CheckBoxSetting("Use Auto Bracket", true));

        settingsService.RegisterSetting("Editor", "Folding", "Editor_UseFolding",
            new CheckBoxSetting("Use Folding", true)
            {
                HoverDescription = "Use Folding in Editor"
            });

        settingsService.RegisterSetting("Editor", "Backups", BackupService.KeyBackupServiceEnable,
            new CheckBoxSetting("Use Automatic Backups",
                ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                HoverDescription = "Use Automatic Backups in case the IDE crashes"
            });
        settingsService.RegisterSetting("Editor", "Backups", BackupService.KeyBackupServiceInterval,
            new ComboBoxSetting("Auto backup interval (s)", 30, new object[] { 5, 10, 15, 30, 60, 120 })
            {
                HoverDescription = "Interval the IDE uses to save files for backup"
            });

        settingsService.RegisterSetting("Editor", "External Changes", "Editor_DetectExternalChanges",
            new CheckBoxSetting("Detect external changes", true)
            {
                HoverDescription = "Detects changes that happen outside of the IDE"
            });
        settingsService.RegisterSetting("Editor", "External Changes", "Editor_NotifyExternalChanges",
            new CheckBoxSetting("Notify external changes", false)
            {
                HoverDescription = "Notifies the user when external happen and ask for reload"
            });

        //TypeAssistance

        settingsService.RegisterSetting("Editor", "Assistance", "TypeAssistance_EnableHover",
            new CheckBoxSetting("Enable Hover Information", true)
            {
                HoverDescription = "Enable Hover Information"
            });
        settingsService.RegisterSetting("Editor", "Assistance", "TypeAssistance_EnableAutoCompletion",
            new CheckBoxSetting("Enable Code Suggestions", true)
            {
                HoverDescription = "Enable completion suggestions"
            });
        settingsService.RegisterSetting("Editor", "Assistance", "TypeAssistance_EnableAutoFormatting",
            new CheckBoxSetting("Enable Auto Formatting", true)
            {
                HoverDescription = "Enable automatic formatting"
            });

        settingsService.RegisterSetting("Editor", "Assistance", "TypeAssistance_DisableLargeFile_Min",
            new SliderSetting("Disable Assistance for Large Files", 1000000, 50000, 10000000, 1000)
            {
                MarkdownDocumentation =
                    "If a document is larger than the specified amount of chars, assistance will be disabled for performance reasons"
            });

        var windowService = Services.Resolve<IWindowService>();
        var commandService = Services.Resolve<IApplicationCommandService>();

        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("Help")
        {
            Header = "Help",
            Priority = 1000
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("Code")
        {
            Header = "Code",
            Priority = 100
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemModel("Changelog")
        {
            Header = "Changelog",
            Icon = new IconModel("VsImageLib2019.StatusUpdateGrey16X"),
            Command = new RelayCommand(() => windowService.Show(new ChangelogView
            {
                DataContext = Services.Resolve<ChangelogViewModel>()
            }))
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemModel("About")
        {
            Header = $"About {paths.AppName}",
            Command = new RelayCommand(() => windowService.Show(new AboutView
            {
                DataContext = Services.Resolve<AboutViewModel>()
            }))
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("Extras")
        {
            Header = "Extras",
            Priority = 900
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemModel("Settings")
        {
            Header = "Settings",
            Icon = new IconModel("Material.SettingsOutline"),
            Command = new AsyncRelayCommand(() => windowService.ShowDialogAsync(new ApplicationSettingsView
            {
                DataContext = Services.Resolve<ApplicationSettingsViewModel>()
            }))
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemModel("Format")
        {
            Header = "Format",
            Icon = new IconModel("BoxIcons.RegularCode"),
            Command = new RelayCommand(
                () => (Services.Resolve<IMainDockService>().CurrentDocument as EditViewModel)?.Format(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is EditViewModel),
            InputGesture = new KeyGesture(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt)
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemModel("Comment Selection")
        {
            Header = "Comment Selection",
            Icon = new IconModel("VsImageLib.CommentCode16X"),
            Command = new RelayCommand(
                () => (Services.Resolve<IMainDockService>().CurrentDocument as EditViewModel)?.TypeAssistance
                    ?.Comment(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is EditViewModel
                {
                    TypeAssistance: not null
                }),
            InputGesture = new KeyGesture(Key.K, KeyModifiers.Control | KeyModifiers.Shift)
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemModel("Uncomment Selection")
        {
            Header = "Uncomment Selection",
            Icon = new IconModel("VsImageLib.UncommentCode16X"),
            Command = new RelayCommand(
                () => (Services.Resolve<IMainDockService>().CurrentDocument as EditViewModel)?.TypeAssistance
                    ?.Uncomment(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is EditViewModel
                {
                    TypeAssistance: not null
                }),
            InputGesture = new KeyGesture(Key.L, KeyModifiers.Control | KeyModifiers.Shift)
        });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemModel("Save")
        {
            Command = new AsyncRelayCommand(
                () => Services.Resolve<IMainDockService>().CurrentDocument!.SaveAsync(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is not null),
            Header = "Save Current",
            InputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey),
            Icon = new IconModel("VsImageLib.Save16XMd")
        });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemModel("Save All")
        {
            Command = new RelayCommand(() =>
            {
                foreach (var file in Services.Resolve<IMainDockService>().OpenFiles) _ = file.Value.SaveAsync();
            }),
            Header = "Save All",
            InputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey | KeyModifiers.Shift),
            Icon = new IconModel("VsImageLib.SaveAll16X")
        });

        var applicationCommandService = Services.Resolve<IApplicationCommandService>();

        applicationCommandService.RegisterCommand(new SimpleApplicationCommand("Active light theme",
            () =>
            {
                settingsService.SetSettingValue("General_SelectedTheme", "Light");
                settingsService.Save(paths.SettingsPath);
            },
            () => settingsService.GetSettingValue<string>("General_SelectedTheme") != "Light"));

        applicationCommandService.RegisterCommand(new SimpleApplicationCommand("Active dark theme",
            () =>
            {
                settingsService.SetSettingValue("General_SelectedTheme", "Dark");
                settingsService.Save(paths.SettingsPath);
            },
            () => settingsService.GetSettingValue<string>("General_SelectedTheme") != "Dark"));
        
        applicationCommandService.RegisterCommand(new SimpleApplicationCommand("Restart",
            () =>
            {
                _ = Services.Resolve<IApplicationStateService>().TryRestartAsync();
            }));

        var welcomeScreenService = Services.Resolve<IWelcomeScreenService>();

        welcomeScreenService.RegisterItemToWalkthrough("fundamentals",
            new WelcomeScreenWalkthroughItem("fundamentals", "Learn the Fundamentals",
                null,
                new RelayCommand(() =>
                {
                    PlatformHelper.OpenHyperLink("https://one-ware.com/docs/studio/tutorials/create-project/");
                }))
            {
                Icon = new IconModel("FluentIconsFilled.LightbulbFilled")
            });

        welcomeScreenService.RegisterItemToWalkthrough("getstarted_oneai",
            new WelcomeScreenWalkthroughItem("getstarted_oneai", "Get Started with ONE AI",
                null,
                new RelayCommand(() =>
                {
                    PlatformHelper.OpenHyperLink("https://one-ware.com/docs/one-ai/getting-started/");
                }))
            {
                Icon = new IconModel("AI_Img")
            });

        //AvaloniaEdit Hyperlink support
        VisualLineLinkText.OpenUriEvent.AddClassHandler<Window>((window, args) =>
        {
            var link = args.Uri.ToString();
            PlatformHelper.OpenHyperLink(link);
        });

        StyledElement shell;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            var mainWindow = Services.Resolve<MainWindow>();
            mainWindow.Closing += (o, i) =>
            {
                var applicationStateService = Services.Resolve<IApplicationStateService>();

                if (!applicationStateService.ShutdownComplete)
                {
                    i.Cancel = true;
                    _ = applicationStateService.TryShutdownAsync();
                }
            };

            shell = mainWindow;
        }
        else
        {
            shell = Services.Resolve<MainSingleView>();
        }

        shell.DataContext = Services.Resolve<MainWindowViewModel>();

        return shell;
    }

    protected virtual void ConfigureModuleCatalog(OneWareModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<SearchListModule>();
        moduleCatalog.AddModule<ErrorListModule>();
        moduleCatalog.AddModule<OutputModule>();
        moduleCatalog.AddModule<ProjectExplorerModule>();
        moduleCatalog.AddModule<LibraryExplorerModule>();
        moduleCatalog.AddModule<FolderProjectSystemModule>();
        moduleCatalog.AddModule<ImageViewerModule>();
        moduleCatalog.AddModule<JsonModule>();
        moduleCatalog.AddModule<TomlModule>();
    }

    protected virtual string GetLogFilePath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
    }

    protected virtual void ConfigureLogging(ILoggingBuilder builder)
    {
        var logPath = GetLogFilePath();
        if (!string.IsNullOrWhiteSpace(logPath))
            Directory.CreateDirectory(logPath);

        var onewareTheme = new SystemConsoleTheme(new Dictionary<ConsoleThemeStyle, SystemConsoleThemeStyle>
        {
            // Main message text
            [ConsoleThemeStyle.Text] = new() { Foreground = ConsoleColor.Cyan },

            // Template / boilerplate
            [ConsoleThemeStyle.SecondaryText] = new() { Foreground = ConsoleColor.DarkGray },
            [ConsoleThemeStyle.TertiaryText] = new() { Foreground = ConsoleColor.DarkGray },

            // Literals / scalars
            [ConsoleThemeStyle.String] = new() { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.Number] = new() { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.Boolean] = new() { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.Null] = new() { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.Scalar] = new() { Foreground = ConsoleColor.White },

            // Property names
            [ConsoleThemeStyle.Name] = new() { Foreground = ConsoleColor.Gray },

            // Invalid output
            [ConsoleThemeStyle.Invalid] = new() { Foreground = ConsoleColor.Red },

            // Log levels (your requirements)
            [ConsoleThemeStyle.LevelVerbose] = new() { Foreground = ConsoleColor.Gray }, // brown
            [ConsoleThemeStyle.LevelDebug] = new() { Foreground = ConsoleColor.DarkYellow },
            [ConsoleThemeStyle.LevelInformation] = new() { Foreground = ConsoleColor.DarkCyan },
            [ConsoleThemeStyle.LevelWarning] = new() { Foreground = ConsoleColor.Yellow },
            [ConsoleThemeStyle.LevelError] = new() { Foreground = ConsoleColor.Red },
            [ConsoleThemeStyle.LevelFatal] = new() { Foreground = ConsoleColor.Magenta }
        });

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logPath, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true
            )
            .WriteTo.File(
                Path.Combine(logPath, "current.txt"),
                shared: true
            )
            .WriteTo.Console(
                theme: onewareTheme
            )
            .CreateLogger();

        builder.ClearProviders();
        builder.AddSerilog(Log.Logger, true);
    }

    protected virtual void LoadStartupPlugins()
    {
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        services.AddSingleton(ModuleCatalog);
        RegisterServices(services);

        ConfigureModuleCatalog(ModuleCatalog);

        _moduleManager = new OneWareModuleManager(ModuleCatalog);
        services.AddSingleton(_moduleManager);
        _moduleManager.RegisterModuleServices(services);

        _moduleServiceRegistry = new ModuleServiceRegistry();
        _moduleServiceRegistry.AddServiceTypes(services);
        services.AddSingleton(_moduleServiceRegistry);

        services.AddSingleton<ICompositeServiceProvider>(sp =>
            new CompositeServiceProvider(
                sp,
                sp.GetRequiredService<ModuleServiceRegistry>()));
        
        var provider = services.BuildServiceProvider();
        var compositeProvider = provider.Resolve<ICompositeServiceProvider>();
        
        ContainerLocator.SetContainer(compositeProvider);

        var logger = compositeProvider.GetRequiredService<ILogger>();
        _moduleManager.SetLogger(logger);

        logger.LogInformation(
            $"App Started: {Global.VersionCode} OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");

        compositeProvider.Resolve<OnnxRuntimeBootstrapper>().Initialize();

        LoadStartupPlugins();

        var shell = CreateShell();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime &&
            shell is Window shellWindow)
            desktopLifetime.MainWindow = shellWindow;
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime &&
                 shell is Control shellView)
            singleViewLifetime.MainView = shellView;

        _moduleManager.InitializeModules(compositeProvider);

        Dispatcher.UIThread.UnhandledException += (s, e) => { Console.WriteLine($"Unhandled: {e.Exception}"); };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            DisableAvaloniaDataAnnotationValidation();

            var mainWindow = Services.Resolve<MainWindow>();
            mainWindow.NotificationManager = new WindowNotificationManager(mainWindow)
            {
                Position = NotificationPosition.TopRight,
                Margin = new Thickness(0, 55, 5, 0),
                BorderThickness = new Thickness(0),
                MaxItems = 3
            };
        }

        Services.Resolve<IApplicationCommandService>().LoadKeyConfiguration();

        Services.Resolve<ISettingsService>().GetSettingObservable<string>("General_SelectedTheme").Subscribe(x =>
        {
            TypeAssistanceIconStore.Instance.Load();
        });

        Services.Resolve<ILogger>().Log("Framework initialization complete!");
        Services.Resolve<BackupService>().LoadAutoSaveFile();
        Services.Resolve<IMainDockService>().LoadLayout(GetDefaultLayoutName);
        Services.Resolve<WelcomeScreenViewModel>().LoadRecentProjects();
        Services.Resolve<BackupService>().Init();

        LoggerExtensions.LayoutLoaded = true;

        Services.Resolve<ISettingsService>().GetSettingObservable<string>("Editor_FontFamily").Subscribe(x =>
        {
            if (FontManager.Current.SystemFonts.Contains(x))
            {
                Resources["EditorFont"] = new FontFamily(x);
                return;
            }

            var findFont = this.TryFindResource(x, out var resourceFont);
            if (findFont && resourceFont is FontFamily fFamily) Resources["EditorFont"] = this.FindResource(x);
        });

        Services.Resolve<ISettingsService>().GetSettingObservable<int>("Editor_FontSize").Subscribe(x =>
        {
            Resources["EditorFontSize"] = (double)x;
        });

        _ = LoadContentAsync();

        base.OnFrameworkInitializationCompleted();
    }

    private static object[] GetOnnxRuntimeOptions(IPaths paths)
    {
        var options = new List<string> { "auto", "cpu", "cuda", "directml", "gpu-linux" };
        try
        {
            if (Directory.Exists(paths.OnnxRuntimesDirectory))
                options.AddRange(Directory.GetDirectories(paths.OnnxRuntimesDirectory)
                    .Select(Path.GetFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))!
                    .Cast<string>());
        }
        catch
        {
            // Ignore IO errors and keep default options.
        }

        return options
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();
    }

    protected virtual Task LoadContentAsync()
    {
        //This is an macOS only feature. On Linux and macOS, we use a different method with file locks inside the Program.cs
        if (this.TryGetFeature<IActivatableLifetime>(out var events))
            events.Activated += (_, args) =>
            {
                if (args is ProtocolActivatedEventArgs { Kind: ActivationKind.OpenUri } protocolArgs)
                    Services.Resolve<IApplicationStateService>().ExecuteUrlLaunchActions(protocolArgs.Uri);
                else if (args is ProtocolActivatedEventArgs { Kind: ActivationKind.File } launchArgs)
                    Services.Resolve<IApplicationStateService>().ExecutePathLaunchActions(launchArgs.Uri.ToString());
            };

        if (Environment.GetEnvironmentVariable("ONEWARE_OPEN_URL") is { } url)
        {
            var uri = new Uri(url);
            Services.Resolve<IApplicationStateService>().ExecuteUrlLaunchActions(uri);
        }

        Services.Resolve<IApplicationStateService>()
            .ExecuteAutoLaunchActions(Environment.GetEnvironmentVariable("ONEWARE_AUTOLAUNCH"));

        return Task.CompletedTask;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) BindingPlugins.DataValidators.Remove(plugin);
    }
}
