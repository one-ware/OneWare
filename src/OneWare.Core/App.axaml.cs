using System;
using System.ComponentModel;
using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneWare.ApplicationCommands.Services;
using OneWare.CloudIntegration;
using OneWare.Core.Adapters;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
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
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using OneWare.Toml;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
using TextMateSharp.Grammars;

namespace OneWare.Core
{
    public class App : PrismApplication
    {
        private readonly ISettingsService _settingsService;
        private readonly IPaths _paths;
        private readonly IWindowService _windowService;
        private readonly IApplicationCommandService _commandService;

        private Autofac.IContainer _autofacContainer;
        private ContainerBuilder _autofacBuilder;

        private TransitionContainerProxy _transitionContainerProxy;

        protected bool _tempMode = false;
        protected AggregateModuleCatalog ModuleCatalog { get; } = new();

        protected virtual string GetDefaultLayoutName => "Default";

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            base.Initialize();
        }

        protected override IContainerExtension CreateContainerExtension()
        {
            // Create the Prism container extension
            var prismContainerExtension = base.CreateContainerExtension();

            // Create the Prism container adapter
            var prismContainerRegistry = (IContainerRegistry)prismContainerExtension;
            var prismContainerProvider = prismContainerExtension as IContainerProvider;

            var prismContainerAdapter = new PrismContainerAdapter(prismContainerRegistry, prismContainerProvider);

            return new TransitionContainerProxy(prismContainerExtension)
            {
                // Optionally set the target container here if you want to transition to another container
                // For example, to transition to Autofac:
                // _targetContainer = new AutofacContainerAdapter(new ContainerBuilder());
            };
        }

        private void RegisterAutofacTypes(ContainerBuilder builder)
        {
            // Services as Singletons
            builder.RegisterType<PluginService>().As<IPluginService>().SingleInstance();
            builder.RegisterType<HttpService>().As<IHttpService>().SingleInstance();
            builder.RegisterType<ApplicationCommandService>().As<IApplicationCommandService>().SingleInstance();
            builder.RegisterType<ProjectManagerService>().As<IProjectManagerService>().SingleInstance();
            builder.RegisterType<LanguageManager>().As<ILanguageManager>().SingleInstance();
            builder.RegisterType<ApplicationStateService>().As<IApplicationStateService>().SingleInstance();
            builder.RegisterType<DockService>().As<IDockService>().SingleInstance();
            builder.RegisterType<WindowService>().As<IWindowService>().SingleInstance();
            builder.RegisterType<ModuleTracker>().As<IModuleTracker>().SingleInstance();
            builder.RegisterType<BackupService>().SingleInstance();
            builder.RegisterType<ChildProcessService>().As<IChildProcessService>().SingleInstance();
            builder.RegisterType<FileIconService>().As<IFileIconService>().SingleInstance();
            builder.RegisterType<EnvironmentService>().As<IEnvironmentService>().SingleInstance();
            builder.RegisterType<PrismContainerAdapter>().As<IContainerAdapter>().SingleInstance();

            // Note: IDockService was registered twice in Prism - only one needed here

            // ViewModels - Singletons
            builder.RegisterType<MainWindowViewModel>().SingleInstance();
            builder.RegisterType<MainDocumentDockViewModel>().SingleInstance();

            // ViewModels - Transients (InstancePerDependency is default)
            builder.RegisterType<WelcomeScreenViewModel>().InstancePerDependency();
            builder.RegisterType<EditViewModel>().InstancePerDependency();
            builder.RegisterType<ChangelogViewModel>().InstancePerDependency();
            builder.RegisterType<AboutViewModel>().InstancePerDependency();

            // Windows - Singletons
            builder.RegisterType<MainWindow>().SingleInstance();
            builder.RegisterType<MainView>().SingleInstance();

            // Add other Autofac registrations as needed
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance<IModuleCatalog>(ModuleCatalog);

            // Services
            containerRegistry.RegisterSingleton<IPluginService, PluginService>();
            containerRegistry.RegisterSingleton<IHttpService, HttpService>();
            containerRegistry.RegisterSingleton<IApplicationCommandService, ApplicationCommandService>();
            containerRegistry.RegisterSingleton<IProjectManagerService, ProjectManagerService>();
            containerRegistry.RegisterSingleton<ILanguageManager, LanguageManager>();
            containerRegistry.RegisterSingleton<IApplicationStateService, ApplicationStateService>();
            containerRegistry.RegisterSingleton<IDockService, DockService>();
            containerRegistry.RegisterSingleton<IWindowService, WindowService>();
            containerRegistry.RegisterSingleton<IModuleTracker, ModuleTracker>();
            containerRegistry.RegisterSingleton<BackupService>();
            containerRegistry.RegisterSingleton<IChildProcessService, ChildProcessService>();
            containerRegistry.RegisterSingleton<IFileIconService, FileIconService>();
            containerRegistry.RegisterSingleton<IEnvironmentService, EnvironmentService>();
            containerRegistry.RegisterSingleton<IContainerAdapter, PrismContainerAdapter>();
            containerRegistry.RegisterSingleton<IDockService, DockService>();
            containerRegistry.RegisterInstance(containerRegistry);

            // ViewModels - Singletons
            containerRegistry.RegisterSingleton<MainWindowViewModel>();
            containerRegistry.RegisterSingleton<MainDocumentDockViewModel>();

            // ViewModels Transients
            containerRegistry.Register<WelcomeScreenViewModel>();
            containerRegistry.Register<EditViewModel>();
            containerRegistry.Register<ChangelogViewModel>();
            containerRegistry.Register<AboutViewModel>();

            // Windows
            containerRegistry.RegisterSingleton<MainWindow>();
            containerRegistry.RegisterSingleton<MainView>();
        }

        protected override AvaloniaObject CreateShell()
        {
            // Register IDE Settings
            var settingsService = Container.Resolve<ISettingsService>();
            var paths = Container.Resolve<IPaths>();

            Name = paths.AppName;

            NativeMenu.SetMenu(this, new NativeMenu
            {
                Items =
                {
                    new NativeMenuItem
                    {
                        Header = $"About {Name}",
                        Command = new RelayCommand(() => Container.Resolve<IWindowService>().Show(
                            new AboutView
                            {
                                DataContext = Container.Resolve<AboutViewModel>()
                            }))
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem
                    {
                        Header = "Settings",
                        Command = new AsyncRelayCommand(() => Container.Resolve<IWindowService>().ShowDialogAsync(
                            new ApplicationSettingsView
                            {
                                DataContext = Container.Resolve<ApplicationSettingsViewModel>()
                            }))
                    }
                }
            });

            // General
            settingsService.RegisterSettingCategory("General", 0, "Material.ToggleSwitchOutline");

            // Editor settings
            settingsService.RegisterSettingCategory("Editor", 0, "BoxIcons.RegularCode");

            settingsService.RegisterSettingCategory("Tools", 0, "FeatherIcons.Tool");

            settingsService.RegisterSettingCategory("Languages", 0, "FluentIcons.ProofreadLanguageRegular");

            settingsService.RegisterSetting("Editor", "Appearance", "Editor_FontFamily",
                new ComboBoxSetting("Editor Font Family", "JetBrains Mono NL", ["JetBrains Mono NL", "IntelOne Mono", "Consolas", "Comic Sans MS", "Fira Code"]));

            settingsService.RegisterSetting("Editor", "Appearance", "Editor_FontSize",
                new ComboBoxSetting("Font Size", 15, Enumerable.Range(10, 30).Cast<object>()));

            settingsService.RegisterSetting("Editor", "Appearance", "Editor_SyntaxTheme_Dark",
                new ComboBoxSetting("Editor Theme Dark", ThemeName.DarkPlus, Enum.GetValues<ThemeName>().Cast<object>())
                {
                    HoverDescription = "Sets the theme for Syntax Highlighting in Dark Mode"
                });

            settingsService.RegisterSetting("Editor", "Appearance", "Editor_SyntaxTheme_Light",
                new ComboBoxSetting("Editor Theme Light", ThemeName.LightPlus, Enum.GetValues<ThemeName>().Cast<object>())
                {
                    HoverDescription = "Sets the theme for Syntax Highlighting in Light Mode"
                });

            settingsService.RegisterSetting("Editor", "Formatting", "Editor_UseAutoFormatting",
                new CheckBoxSetting("Use Auto Formatting", true));

            settingsService.RegisterSetting("Editor", "Formatting", "Editor_UseAutoBracket", new CheckBoxSetting("Use Auto Bracket", true));

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

            // TypeAssistance
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableHover",
                "Enable Hover Information", "Enable Hover Information", true);
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoCompletion",
                "Enable Code Suggestions", "Enable completion suggestions", true);
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoFormatting",
                "Enable Auto Formatting", "Enable automatic formatting", true);

            var windowService = Container.Resolve<IWindowService>();
            var commandService = Container.Resolve<IApplicationCommandService>();

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
                    DataContext = Container.Resolve<ChangelogViewModel>()
                }))
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("About")
            {
                Header = $"About {paths.AppName}",
                Command = new RelayCommand(() => windowService.Show(new AboutView
                {
                    DataContext = Container.Resolve<AboutViewModel>()
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
                    DataContext = ContainerLocator.Container.Resolve<ApplicationSettingsViewModel>()
                }))
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemViewModel("Format")
            {
                Header = "Format",
                IconObservable = Current!.GetResourceObservable("BoxIcons.RegularCode"),
                Command = new RelayCommand(
                    () => (Container.Resolve<IDockService>().CurrentDocument as EditViewModel)?.Format(),
                    () => Container.Resolve<IDockService>().CurrentDocument is EditViewModel),
                InputGesture = new KeyGesture(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt)
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemViewModel("Comment Selection")
            {
                Header = "Comment Selection",
                IconObservable = Current!.GetResourceObservable("VsImageLib.CommentCode16X"),
                Command = new RelayCommand(
                    () => (Container.Resolve<IDockService>().CurrentDocument as EditViewModel)?.TypeAssistance?.Comment(),
                    () => Container.Resolve<IDockService>().CurrentDocument is EditViewModel { TypeAssistance: not null }),
                InputGesture = new KeyGesture(Key.K, KeyModifiers.Control | KeyModifiers.Shift)
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemViewModel("Uncomment Selection")
            {
                Header = "Uncomment Selection",
                IconObservable = Current!.GetResourceObservable("VsImageLib.UncommentCode16X"),
                Command = new RelayCommand(
                    () => (Container.Resolve<IDockService>().CurrentDocument as EditViewModel)?.TypeAssistance?.Uncomment(),
                    () => Container.Resolve<IDockService>().CurrentDocument is EditViewModel { TypeAssistance: not null }),
                InputGesture = new KeyGesture(Key.L, KeyModifiers.Control | KeyModifiers.Shift)
            });

            windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemViewModel("Save")
            {
                Command = new AsyncRelayCommand(
                    () => Container.Resolve<IDockService>().CurrentDocument!.SaveAsync(),
                    () => Container.Resolve<IDockService>().CurrentDocument is not null),
                Header = "Save Current",
                InputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey),
                IconObservable = Current!.GetResourceObservable("VsImageLib.Save16XMd")
            });

            windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemViewModel("Save All")
            {
                Command = new RelayCommand(
                    () =>
                    {
                        foreach (var file in Container.Resolve<IDockService>().OpenFiles) _ = file.Value.SaveAsync();
                    }),
                Header = "Save All",
                InputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey | KeyModifiers.Shift),
                IconObservable = Current!.GetResourceObservable("VsImageLib.SaveAll16X")
            });

            var applicationCommandService = Container.Resolve<IApplicationCommandService>();

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

            // AvaloniaEdit Hyperlink support
            VisualLineLinkText.OpenUriEvent.AddClassHandler<Window>((window, args) =>
            {
                var link = args.Uri.ToString();
                PlatformHelper.OpenHyperLink(link);
            });

            if (ApplicationLifetime is ISingleViewApplicationLifetime)
            {
                var mainView = Container.Resolve<MainView>();
                mainView.DataContext = ContainerLocator.Container.Resolve<MainWindowViewModel>();
                return mainView;
            }

            var mainWindow = Container.Resolve<MainWindow>();

            mainWindow.Closing += (o, i) => _ = TryShutDownAsync(o, i);

            return mainWindow;
        }

        protected override IModuleCatalog CreateModuleCatalog()
        {
            return ModuleCatalog;
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
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

            base.ConfigureModuleCatalog(moduleCatalog);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog();
            });

            var logger = loggerFactory.CreateLogger<App>();
            logger.LogInformation("Starting framework initialization...");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                DisableAvaloniaDataAnnotationValidation();

                var mainWindow = Container.Resolve<MainWindow>();
                mainWindow.NotificationManager = new WindowNotificationManager(mainWindow)
                {
                    Position = NotificationPosition.TopRight,
                    Margin = new Thickness(0, 55, 5, 0),
                    BorderThickness = new Thickness(0),
                    MaxItems = 3
                };
            }

            Container.Resolve<IApplicationCommandService>().LoadKeyConfiguration();

            Container.Resolve<ISettingsService>().GetSettingObservable<string>("General_SelectedTheme").Subscribe(x =>
            {
                TypeAssistanceIconStore.Instance.Load();
            });

            Container.Resolve<Essentials.Services.ILogger>().Log("Framework initialization complete!", ConsoleColor.Green);
            Container.Resolve<BackupService>().LoadAutoSaveFile();
            Container.Resolve<IDockService>().LoadLayout(GetDefaultLayoutName);
            Container.Resolve<BackupService>().Init();

            Container.Resolve<ISettingsService>().GetSettingObservable<string>("Editor_FontFamily").Subscribe(x =>
            {
                if (FontManager.Current.SystemFonts.Contains(x))
                {
                    Resources["EditorFont"] = new FontFamily(x);
                    return;
                }

                var findFont = this.TryFindResource(x, out var resourceFont);
                if (findFont && resourceFont is FontFamily fFamily) Resources["EditorFont"] = this.FindResource(x);
            });

            Container.Resolve<ISettingsService>().GetSettingObservable<int>("Editor_FontSize").Subscribe(x =>
            {
                Resources["EditorFontSize"] = (double)x;
            });

            _ = LoadContentAsync();

            base.OnFrameworkInitializationCompleted();
        }

        protected virtual Task LoadContentAsync()
        {
            return Task.CompletedTask;
        }

        private async Task TryShutDownAsync(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            try
            {
                var unsavedFiles = new List<IExtendedDocument>();

                foreach (var tab in Container.Resolve<IDockService>().OpenFiles)
                    if (tab.Value is { IsDirty: true } evm)
                        unsavedFiles.Add(evm);

                var mainWin = MainWindow as Window;
                if (mainWin == null) throw new NullReferenceException(nameof(mainWin));
                var shutdownReady = await WindowHelper.HandleUnsavedFilesAsync(unsavedFiles, mainWin);

                if (shutdownReady) await ShutdownAsync();
            }
            catch (Exception ex)
            {
                Container.Resolve<Essentials.Services.ILogger>().Error(ex.Message, ex);
            }
        }

        private async Task ShutdownAsync()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime cds)
                foreach (var win in cds.Windows)
                    win.Hide();

            Container.Resolve<BackupService>().CleanUp();

            await Container.Resolve<LanguageManager>().CleanResourcesAsync();

            if (!_tempMode) await Container.Resolve<IProjectExplorerService>().SaveLastProjectsFileAsync();

            Container.Resolve<Essentials.Services.ILogger>()?.Log("Closed!", ConsoleColor.DarkGray);

            if (!_tempMode) Container.Resolve<IDockService>().SaveLayout();

            Container.Resolve<ISettingsService>().Save(Container.Resolve<IPaths>().SettingsPath);

            Container.Resolve<IApplicationStateService>().ExecuteShutdownActions();

            Environment.Exit(0);
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
