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
using Example;
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
using Serilog;
using TextMateSharp.Grammars;
using OneWare.Core.ViewModels;
using OneWare.Core.Views;

namespace OneWare.Core
{
    public class App : Application
    {
        private readonly ILogger<App> _logger;
        private readonly AggregateModuleCatalog _aggregateModuleCatalog;
        private readonly BackupService _backupService;
        private readonly IPaths _paths;
        private readonly IWindowService _windowService;
        private readonly ISettingsService _settingsService;
        private readonly IProjectExplorerService _projectExplorerService;
        private readonly IOutputService _outputService;
        private readonly IPluginService _pluginService;
        private readonly IEnvironmentService _environmentService;
        private readonly ILanguageManager _languageManager;
        private readonly IDockService _dockService;
        private readonly IApplicationStateService _applicationStateService;
        private readonly IChildProcessService _childProcessService;
        private readonly PlatformHelper _platformHelper;


        public static IContainerAdapter ContainerAdapter { get; private set; }
        
        protected bool _tempMode = false;
        

        protected virtual string GetDefaultLayoutName => "Default";

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            InitializeContainer();
        }

        public App(ILogger<App> logger, 
                   AggregateModuleCatalog aggregateModuleCatalog,
                   IDockService dockService,
                   ISettingsService settingsService,
                   IWindowService windowService,
                   PlatformHelper platformHelper,
                   BackupService backupService,
                   IPaths paths)
        {
            // Start with lightweight container
            var initial = new PureContainerAdapter();

            // Proxy to defer actual container switch
            ContainerAdapter = new ContainerTransitionProxy(initial);
            _logger = logger;
            _aggregateModuleCatalog = aggregateModuleCatalog;
            _paths = paths;
            _dockService = dockService;
            _settingsService = settingsService;
            _windowService = windowService;
            _platformHelper = platformHelper;
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

        }

        protected virtual void InitializeContainer()
        {
            // Create and configure the Autofac container
            var autofacAdapter = new AutofacContainerAdapter();
            
            // Register services
            RegisterServices(autofacAdapter);
            
            // Build the container
            autofacAdapter.Build();
            
            // Update the container adapter
            ContainerAdapter = autofacAdapter;

            // Configure settings and menu
            ConfigureSettings(autofacAdapter);
        }

        protected virtual void RegisterServices(IContainerAdapter container)
        {
            // Register core services
            container.Register<ISettingsService, SettingsService>(isSingleton: true);
            container.Register<IPaths, Paths>(isSingleton: true);
            container.Register<IWindowService, WindowService>(isSingleton: true);
            container.Register<IApplicationCommandService, ApplicationCommandService>(isSingleton: true);
            container.Register<IPluginService, PluginService>(isSingleton: true);
            container.Register<IHttpService, HttpService>(isSingleton: true);
            container.Register<IProjectManagerService, ProjectManagerService>(isSingleton: true);
            container.Register<ILanguageManager, LanguageManager>(isSingleton: true);
            container.Register<IApplicationStateService, ApplicationStateService>(isSingleton: true);
            container.Register<IDockService, DockService>(isSingleton: true);
            container.Register<IModuleTracker, ModuleTracker>(isSingleton: true);
            container.RegisterInstance<BackupService>(_backupService);
            container.Register<IChildProcessService, ChildProcessService>(isSingleton: true);
            container.Register<IFileIconService, FileIconService>(isSingleton: true);
            container.Register<IEnvironmentService, EnvironmentService>(isSingleton: true);

            // ViewModels - Singletons
            container.Register<MainWindowViewModel, MainWindowViewModel>(isSingleton: true);
            container.Register<MainDocumentDockViewModel, MainDocumentDockViewModel>(isSingleton: true);

            // ViewModels Transients
            container.Register<WelcomeScreenViewModel, WelcomeScreenViewModel>();
            container.Register<EditViewModel, EditViewModel>();
            container.Register<ChangelogViewModel, ChangelogViewModel>();
            container.Register<AboutViewModel, AboutViewModel>();
            container.Register<ApplicationSettingsViewModel, ApplicationSettingsViewModel>();

            // Windows
            container.Register<MainWindow, MainWindow>(isSingleton: true);
            container.Register<MainView, MainView>(isSingleton: true);
            container.Register<AboutView, AboutView>();
            container.Register<ApplicationSettingsView, ApplicationSettingsView>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = ContainerAdapter.Resolve<MainWindow>();
                
                DisableAvaloniaDataAnnotationValidation();
                
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    var notificationManager = new WindowNotificationManager(mainWindow)
                    {
                        Position = NotificationPosition.TopRight,
                        Margin = new Thickness(0, 55, 5, 0),
                        BorderThickness = new Thickness(0),
                        MaxItems = 3
                    };
                    mainWindow.Resources["NotificationManager"] = notificationManager;
                }
            }

            // Subscribe to font settings changes
            ContainerAdapter.Resolve<ISettingsService>().GetSettingObservable<string>("Editor_FontFamily").Subscribe(x =>
            {
                if (FontManager.Current.SystemFonts.Contains(x))
                {
                    Resources["EditorFont"] = new FontFamily(x);
                    return;
                }

                var findFont = this.TryFindResource(x, out var resourceFont);
                if (findFont && resourceFont is FontFamily) Resources["EditorFont"] = this.FindResource(x);
            });

            ContainerAdapter.Resolve<ISettingsService>().GetSettingObservable<int>("Editor_FontSize").Subscribe(x =>
            {
                Resources["EditorFontSize"] = (double)x;
            });

            _ = LoadContentAsync();

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureSettings(IContainerAdapter container)
        {
            var settingsService = container.Resolve<ISettingsService>();
            var paths = container.Resolve<IPaths>();

            Name = paths.AppName;

            NativeMenu.SetMenu(this, new NativeMenu
            {
                Items =
                {
                    new NativeMenuItem
                    {
                        Header = $"About {Name}",
                        Command = new RelayCommand(() => container.Resolve<IWindowService>().Show(
                            new AboutView
                            {
                                DataContext = container.Resolve<AboutViewModel>()
                            }))
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem
                    {
                        Header = "Settings",
                        Command = new AsyncRelayCommand(() => container.Resolve<IWindowService>().ShowDialogAsync(
                            new ApplicationSettingsView
                            {
                                DataContext = container.Resolve<ApplicationSettingsViewModel>()
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
                new ComboBoxSetting("Editor Theme Dark", ThemeName.DarkPlus, Enum.GetValues<ThemeName>().Cast<object>()));

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

                foreach (var tab in ContainerAdapter.Resolve<IDockService>().OpenFiles)
                {
                    if (tab.Value is { IsDirty: true } evm)
                        unsavedFiles.Add(evm);
                }

                var mainWin = ContainerAdapter.Resolve<MainWindow>() as Window;
                if (mainWin == null) throw new NullReferenceException(nameof(mainWin));

                var windowHelper = ContainerAdapter.Resolve<WindowHelper>();
                var shutdownReady = await windowHelper.HandleUnsavedFilesAsync(unsavedFiles, mainWin);

                if (shutdownReady) await ShutdownAsync();
            }
            catch (Exception ex)
            {
                ContainerAdapter.Resolve<Essentials.Services.ILogger>().Error(ex.Message, ex);
            }
        }

        private async Task ShutdownAsync()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime cds)
                foreach (var win in cds.Windows)
                    win.Hide();

            ContainerAdapter.Resolve<BackupService>().CleanUp();

            await ContainerAdapter.Resolve<LanguageManager>().CleanResourcesAsync();

            if (!_tempMode) await ContainerAdapter.Resolve<IProjectExplorerService>().SaveLastProjectsFileAsync();

            ContainerAdapter.Resolve<Essentials.Services.ILogger>()?.Log("Closed!", ConsoleColor.DarkGray);

            if (!_tempMode) ContainerAdapter.Resolve<IDockService>().SaveLayout();

            ContainerAdapter.Resolve<ISettingsService>().Save(ContainerAdapter.Resolve<IPaths>().SettingsPath);

            ContainerAdapter.Resolve<IApplicationStateService>().ExecuteShutdownActions();

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
