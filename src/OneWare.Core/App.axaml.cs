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
using OneWare.ApplicationCommands.Services;
using OneWare.CloudIntegration;
using OneWare.Core.Models;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Extensions;
using OneWare.Core.Views.Windows;
using OneWare.Debugger;
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
using TextMateSharp.Grammars;

namespace OneWare.Core;

public class App : Application
{
    protected OneWareModuleCatalog ModuleCatalog { get; } = new();

    protected virtual string GetDefaultLayoutName => "Default";

    protected IServiceProvider Services => ContainerLocator.Current;

    private OneWareModuleManager? _moduleManager;
    private ModuleServiceRegistry? _moduleServiceRegistry;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        Animation.RegisterCustomAnimator<string, CustomStringAnimator>();
    }

    protected virtual void RegisterServices(IServiceCollection services)
    {
        //Services
        services.AddSingleton<IPluginService, PluginService>();
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<IApplicationCommandService, ApplicationCommandService>();
        services.AddSingleton<IProjectManagerService, ProjectManagerService>();
        services.AddSingleton<ILanguageManager, LanguageManager>();
        services.AddSingleton<IApplicationStateService, ApplicationStateService>();
        services.AddSingleton<IMainDockService, MainDockService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IModuleTracker, ModuleTracker>();
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
        services.AddSingleton<MainView>();
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

        //Editor settings
        settingsService.RegisterSettingCategory("Editor", 0, "BoxIcons.RegularCode");

        settingsService.RegisterSettingCategory("Tools", 0, "FeatherIcons.Tool");

        settingsService.RegisterSettingCategory("Languages", 0, "FluentIcons.ProofreadLanguageRegular");

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_FontFamily",
            new ComboBoxSetting("Editor Font Family", "JetBrains Mono NL",
                ["JetBrains Mono NL", "IntelOne Mono", "Consolas", "Comic Sans MS", "Fira Code"]));

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_FontSize",
            new ComboBoxSetting("Font Size", 15, Enumerable.Range(10, 30).Cast<object>()));

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_SyntaxTheme_Dark",
            new ComboBoxSetting("Editor Theme Dark", ThemeName.DarkPlus, Enum.GetValues<ThemeName>().Cast<object>().ToArray())
            {
                HoverDescription = "Sets the theme for Syntax Highlighting in Dark Mode"
            });

        settingsService.RegisterSetting("Editor", "Appearance", "Editor_SyntaxTheme_Light",
            new ComboBoxSetting("Editor Theme Light", ThemeName.LightPlus, Enum.GetValues<ThemeName>().Cast<object>().ToArray())
            {
                HoverDescription = "Sets the theme for Syntax Highlighting in Light Mode"
            });

        settingsService.RegisterSetting("Editor", "Formatting", "Editor_UseAutoFormatting",
            new CheckBoxSetting("Use Auto Formatting", true));

        settingsService.RegisterSetting("Editor", "Formatting", "Editor_UseAutoBracket",
            new CheckBoxSetting("Use Auto Bracket", true));

        settingsService.RegisterTitled("Editor", "Folding", "Editor_UseFolding", "Use Folding",
            "Use Folding in Editor", true);

        settingsService.RegisterTitled("Editor", "Backups", BackupService.KeyBackupServiceEnable,
            "Use Automatic Backups", "Use Automatic Backups in case the IDE crashes",
            ApplicationLifetime is IClassicDesktopStyleApplicationLifetime);
        settingsService.RegisterTitledCombo("Editor", "Backups", BackupService.KeyBackupServiceInterval,
            "Auto backup interval (s)",
            "Interval the IDE uses to save files for backup", 30, 5, 10, 15, 30, 60, 120);

        settingsService.RegisterTitled("Editor", "External Changes", "Editor_DetectExternalChanges",
            "Detect external changes", "Detects changes that happen outside of the IDE", true);
        settingsService.RegisterTitled("Editor", "External Changes", "Editor_NotifyExternalChanges",
            "Notify external changes", "Notifies the user when external happen and ask for reload", false);

        //TypeAssistance

        settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableHover",
            "Enable Hover Information", "Enable Hover Information", true);
        settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoCompletion",
            "Enable Code Suggestions", "Enable completion suggestions", true);
        settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoFormatting",
            "Enable Auto Formatting", "Enable automatic formatting", true);

        settingsService.RegisterSetting("Editor", "Assistance", "TypeAssistance_DisableLargeFile_Min",
            new SliderSetting("Disable Assistance for Large Files", 100000, 50000, 1000000, 1000)
            {
                MarkdownDocumentation =
                    "If a document is larger than the specified amount of chars, assistance will be disabled for performance reasons"
            });

        var windowService = Services.Resolve<IWindowService>();
        var commandService = Services.Resolve<IApplicationCommandService>();

        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemViewModel("Help")
        {
            Header = "Help",
            Priority = 1000
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemViewModel("Code")
        {
            Header = "Code",
            Priority = 100
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("Changelog")
        {
            Header = "Changelog",
            IconObservable = Current!.GetResourceObservable("VsImageLib2019.StatusUpdateGrey16X"),
            Command = new RelayCommand(() => windowService.Show(new ChangelogView
            {
                DataContext = Services.Resolve<ChangelogViewModel>()
            }))
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("About")
        {
            Header = $"About {paths.AppName}",
            Command = new RelayCommand(() => windowService.Show(new AboutView
            {
                DataContext = Services.Resolve<AboutViewModel>()
            }))
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemViewModel("Extras")
        {
            Header = "Extras",
            Priority = 900
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemViewModel("Settings")
        {
            Header = "Settings",
            IconObservable = Current!.GetResourceObservable("Material.SettingsOutline"),
            Command = new AsyncRelayCommand(() => windowService.ShowDialogAsync(new ApplicationSettingsView
            {
                DataContext = Services.Resolve<ApplicationSettingsViewModel>()
            }))
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemViewModel("Format")
        {
            Header = "Format",
            IconObservable = Current!.GetResourceObservable("BoxIcons.RegularCode"),
            Command = new RelayCommand(
                () => (Services.Resolve<IMainDockService>().CurrentDocument as EditViewModel)?.Format(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is EditViewModel),
            InputGesture = new KeyGesture(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt)
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemViewModel("Comment Selection")
        {
            Header = "Comment Selection",
            IconObservable = Current!.GetResourceObservable("VsImageLib.CommentCode16X"),
            Command = new RelayCommand(
                () => (Services.Resolve<IMainDockService>().CurrentDocument as EditViewModel)?.TypeAssistance?.Comment(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is EditViewModel { TypeAssistance: not null }),
            InputGesture = new KeyGesture(Key.K, KeyModifiers.Control | KeyModifiers.Shift)
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemViewModel("Uncomment Selection")
        {
            Header = "Uncomment Selection",
            IconObservable = Current!.GetResourceObservable("VsImageLib.UncommentCode16X"),
            Command = new RelayCommand(
                () => (Services.Resolve<IMainDockService>().CurrentDocument as EditViewModel)?.TypeAssistance?.Uncomment(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is EditViewModel { TypeAssistance: not null }),
            InputGesture = new KeyGesture(Key.L, KeyModifiers.Control | KeyModifiers.Shift)
        });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemViewModel("Save")
        {
            Command = new AsyncRelayCommand(
                () => Services.Resolve<IMainDockService>().CurrentDocument!.SaveAsync(),
                () => Services.Resolve<IMainDockService>().CurrentDocument is not null),
            Header = "Save Current",
            InputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey),
            IconObservable = Current!.GetResourceObservable("VsImageLib.Save16XMd")
        });

        windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemViewModel("Save All")
        {
            Command = new RelayCommand(
                () =>
                {
                    foreach (var file in Services.Resolve<IMainDockService>().OpenFiles) _ = file.Value.SaveAsync();
                }),
            Header = "Save All",
            InputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey | KeyModifiers.Shift),
            IconObservable = Current!.GetResourceObservable("VsImageLib.SaveAll16X")
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

        var welcomeScreenService = Services.Resolve<IWelcomeScreenService>();

        welcomeScreenService.RegisterItemToWalkthrough("fundamentals",
            new WelcomeScreenWalkthroughItem("fundamentals", "Learn the Fundamentals",
                null, new RelayCommand(() =>
                {
                    PlatformHelper.OpenHyperLink("https://one-ware.com/docs/studio/tutorials/create-project/");
                }))
            {
                IconObservable = Application.Current!.GetResourceObservable("FluentIconsFilled.LightbulbFilled")
            });

        welcomeScreenService.RegisterItemToWalkthrough("getstarted_oneai",
            new WelcomeScreenWalkthroughItem("getstarted_oneai", "Get Started with OneAI",
                null, new RelayCommand(() =>
                {
                    PlatformHelper.OpenHyperLink("https://one-ware.com/docs/one-ai/getting-started/");
                }))
            {
                IconObservable = Application.Current!.GetResourceObservable("AI_Img"),
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
            shell = Services.Resolve<MainView>();
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
        moduleCatalog.AddModule<DebuggerModule>();
        moduleCatalog.AddModule<OneWareCloudIntegrationModule>();
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
        services.AddSingleton(_moduleServiceRegistry);

        var rootProvider = services.BuildServiceProvider();
        var compositeProvider = new CompositeServiceProvider(rootProvider, _moduleServiceRegistry);

        ContainerLocator.SetContainer(compositeProvider);
        
        _moduleManager.SetLogger(compositeProvider.Resolve<ILogger>());

        LoadStartupPlugins();

        var shell = CreateShell();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime &&
            shell is Window shellWindow)
        {
            desktopLifetime.MainWindow = shellWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime &&
                 shell is Control shellView)
        {
            singleViewLifetime.MainView = shellView;
        }

        _moduleManager.InitializeModules(compositeProvider);

        Dispatcher.UIThread.UnhandledException += (s, e) =>
        {
            Console.WriteLine($"Unhandled: {e.Exception}");
        };

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

        Services.Resolve<ILogger>().Log("Framework initialization complete!", ConsoleColor.Green);
        Services.Resolve<BackupService>().LoadAutoSaveFile();
        Services.Resolve<IMainDockService>().LoadLayout(GetDefaultLayoutName);
        Services.Resolve<WelcomeScreenViewModel>().LoadRecentProjects();
        Services.Resolve<BackupService>().Init();

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

    protected virtual Task LoadContentAsync()
    {
        //This is an macOS only feature. On Linux and macOS, we use a different method with file locks inside the Program.cs
        if (this.TryGetFeature<IActivatableLifetime>(out var events))
        {
            events.Activated += (_, args) =>
            {
                if (args is ProtocolActivatedEventArgs { Kind: ActivationKind.OpenUri } protocolArgs)
                {
                    Services.Resolve<IApplicationStateService>().ExecuteUrlLaunchActions(protocolArgs.Uri);
                }
                else if (args is ProtocolActivatedEventArgs { Kind: ActivationKind.File } launchArgs)
                {
                    Services.Resolve<IApplicationStateService>().ExecutePathLaunchActions(launchArgs.Uri.ToString());
                }
            };
        }

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
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
