using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Rendering;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using OneWare.ApplicationCommands;
using OneWare.ApplicationCommands.Models;
using OneWare.ApplicationCommands.Services;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.ErrorList;
using OneWare.FolderProjectSystem;
using OneWare.ImageViewer;
using OneWare.Output;
using OneWare.ProjectExplorer;
using OneWare.ProjectSystem.Services;
using OneWare.SearchList;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Essentials;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using TextMateSharp.Grammars;

namespace OneWare.Core
{
    public class App : PrismApplication
    {
        protected AggregateModuleCatalog ModuleCatalog { get; } = new();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            base.Initialize();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance<IModuleCatalog>(ModuleCatalog);
            
            //Services
            containerRegistry.RegisterSingleton<IPluginService, PluginService>();
            containerRegistry.RegisterSingleton<IHttpService, HttpService>();
            containerRegistry.RegisterSingleton<IApplicationCommandService, ApplicationCommandService>();
            containerRegistry.RegisterSingleton<IProjectManagerService, ProjectManagerService>();
            containerRegistry.RegisterSingleton<ILanguageManager, LanguageManager>();
            containerRegistry.RegisterSingleton<IApplicationStateService, ApplicationStateService>();
            containerRegistry.RegisterSingleton<INativeToolService, NativeToolService>();
            containerRegistry.RegisterSingleton<IDockService, DockService>();
            containerRegistry.RegisterSingleton<IWindowService, WindowService>();
            containerRegistry.RegisterSingleton<IModuleTracker, ModuleTracker>();
            containerRegistry.RegisterSingleton<BackupService>();
            containerRegistry.RegisterSingleton<IChildProcessService, ChildProcessService>();

            //ViewModels - Windows
            containerRegistry.RegisterSingleton<MainWindowViewModel>();
            containerRegistry.RegisterSingleton<ApplicationSettingsViewModel>();
            containerRegistry.RegisterSingleton<ChangelogViewModel>();
            containerRegistry.RegisterSingleton<AboutViewModel>();

            //ViewModels - Dock
            containerRegistry.RegisterSingleton<WelcomeScreenViewModel>();
            containerRegistry.RegisterSingleton<MainDocumentDockViewModel>();

            //ViewModels Documents
            containerRegistry.Register<EditViewModel>();

            //Windows
            containerRegistry.RegisterSingleton<MainWindow>();
            containerRegistry.RegisterSingleton<MainView>();
        }

        protected override AvaloniaObject CreateShell()
        {
            //Register IDE Settings
            var settingsService = Container.Resolve<ISettingsService>();
            var paths = Container.Resolve<IPaths>();

            Name = paths.AppName;

            //General
            settingsService.RegisterSettingCategory("General", 0, "Material.ToggleSwitchOutline");

            //Editor settings
            settingsService.RegisterSettingCategory("Editor", 0, "BoxIcons.RegularCode");
            
            settingsService.RegisterSettingCategory("Tools", 0, "FeatherIcons.Tool");
            
            settingsService.RegisterSettingCategory("Languages", 0, "FluentIcons.ProofreadLanguageRegular");

            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_FontFamily", "Font",
                "Editor Font Family",
                "JetBrains Mono NL",
                "JetBrains Mono NL", "IntelOne Mono", "Consolas", "Comic Sans MS", "Fira Code");

            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_FontSize", "Font Size",
                "Editor Font Size", 15, Enumerable.Range(10, 30).ToArray());

            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_SyntaxTheme_Dark", "Editor Theme Dark",
                "Setts the theme for Syntax Highlighting in Dark Mode", ThemeName.DarkPlus,
                Enum.GetValues<ThemeName>());

            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_SyntaxTheme_Light",
                "Editor Theme Light",
                "Setts the theme for Syntax Highlighting in Light Mode", ThemeName.LightPlus,
                Enum.GetValues<ThemeName>());

            //settingsService.RegisterTitled("Editor", "Appearance", "Editor_ErrorMarking_Mode", "Error Marking mode"); dfdf 

            settingsService.RegisterTitled("Editor", "Formatting", "Editor_UseAutoFormatting", "Use Auto Formatting",
                "Use Auto Formatting in Editor", true);
            settingsService.RegisterTitled("Editor", "Formatting", "Editor_UseAutoBracket", "Use Auto Bracket",
                "Use Auto Bracket in Editor", true);

            settingsService.RegisterTitled("Editor", "Folding", "Editor_UseFolding", "Use Folding",
                "Use Folding in Editor", true);

            settingsService.RegisterTitled("Editor", "Backups", BackupService.KeyBackupServiceEnable,
                "Use Automatic Backups", "Use Automatic Backups in case the IDE crashes",
                ApplicationLifetime is IClassicDesktopStyleApplicationLifetime);
            settingsService.RegisterTitledCombo("Editor", "Backups", BackupService.KeyBackupServiceInterval,
                "Auto backup interval (s)",
                "Interval the IDE uses to save files for backup", 30, 5, 10, 15, 30, 60, 120);

            settingsService.RegisterTitled("Editor", "External Changes", "Editor_DetectExternalChanges",
                "Detect external changes", "", true);
            settingsService.RegisterTitled("Editor", "External Changes", "Editor_NotifyExternalChanges",
                "Notify external changes", "", false);

            //TypeAssistance
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableHover",
                "Enable Hover Information", "Enable Hover Information", true);
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoCompletion",
                "Enable Code Suggestions", "Enable completion suggestions", true);
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoFormatting",
                "Enable Auto Formatting", "Enable automatic formatting", true);

            settingsService.Load(Container.Resolve<IPaths>().SettingsPath);

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
                Header = $"Changelog",
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib2019.StatusUpdateGrey16X"),
                Command = new RelayCommand(() => windowService.Show(new ChangelogView()
                {
                    DataContext = Container.Resolve<ChangelogViewModel>()
                }))
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel("About")
            {
                Header = $"About {paths.AppName}",
                Command = new RelayCommand(() => windowService.Show(new AboutView()
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
                Header = $"Settings",
                IconObservable = Current!.GetResourceObservable("Material.SettingsOutline"),
                Command = new RelayCommand(() => windowService.Show(new ApplicationSettingsView()
                {
                    DataContext = ContainerLocator.Container.Resolve<ApplicationSettingsViewModel>()
                }))
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Code", new MenuItemViewModel("Format")
            {
                Header = $"Format",
                IconObservable = Current!.GetResourceObservable("BoxIcons.RegularCode"),
                Command = new RelayCommand(
                    () => (Container.Resolve<IDockService>().CurrentDocument as EditViewModel)?.Format(), 
                    () => Container.Resolve<IDockService>().CurrentDocument is EditViewModel),
                InputGesture = new KeyGesture(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt),
            });
            
            windowService.RegisterMenuItem("MainWindow_MainMenu/File", new MenuItemViewModel("Save")
            {
                Command = new AsyncRelayCommand(
                    () => Container.Resolve<IDockService>().CurrentDocument?.SaveAsync() ?? Task.FromResult(false), 
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
                        foreach (var file in Container.Resolve<IDockService>().OpenFiles)
                        {
                            _ = file.Value.SaveAsync();
                        }
                    }),
                Header = "Save All",
                InputGesture = new KeyGesture(Key.S, PlatformHelper.ControlKey | KeyModifiers.Shift),
                IconObservable = Current!.GetResourceObservable("VsImageLib.SaveAll16X")
            });

            //AvaloniaEdit Hyperlink support
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
            else
            {
                var mainWindow = Container.Resolve<MainWindow>();

                mainWindow.Closing += (o, i) => _ = TryShutDownAsync(o, i);

                return mainWindow;
            }
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
            moduleCatalog.AddModule<FolderProjectSystemModule>();
            moduleCatalog.AddModule<ImageViewerModule>();

            base.ConfigureModuleCatalog(moduleCatalog);
        }

        protected virtual string GetDefaultLayoutName => "Default";

        public override void OnFrameworkInitializationCompleted()
        {
            Container.Resolve<ISettingsService>().Load(Container.Resolve<IPaths>().SettingsPath);
            Container.Resolve<IApplicationCommandService>().LoadKeyConfiguration();
            
            Container.Resolve<ISettingsService>().GetSettingObservable<string>("General_SelectedTheme").Subscribe(x =>
            {
                TypeAssistanceIconStore.Instance.Load();
            });

            Container.Resolve<ILogger>().Log("Framework initialization complete!", ConsoleColor.Green);
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
                if (findFont && resourceFont is FontFamily fFamily)
                {
                    Resources["EditorFont"] = this.FindResource(x);
                }
            });

            Container.Resolve<ISettingsService>().GetSettingObservable<int>("Editor_FontSize").Subscribe(x =>
            {
                Resources["EditorFontSize"] = x;
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
                Container.Resolve<ILogger>().Error(ex.Message, ex);
            }
        }

        private async Task ShutdownAsync()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime cds)
                foreach (var win in cds.Windows)
                    win.Hide();

            //DockService.ProjectFiles.SaveLastProjectData();
            Container.Resolve<BackupService>().CleanUp();

            await Container.Resolve<LanguageManager>().CleanResourcesAsync();

            await Container.Resolve<IProjectExplorerService>().SaveLastProjectsFileAsync();

            //if (LaunchUpdaterOnExit) Global.PackageManagerViewModel.VhdPlusUpdaterModel.LaunchUpdater(); TODO

            Container.Resolve<ILogger>()?.Log("Closed!", ConsoleColor.DarkGray);

            //Save active layout
            Container.Resolve<IDockService>().SaveLayout();

            //Save settings
            Container.Resolve<ISettingsService>().Save(Container.Resolve<IPaths>().SettingsPath);

            Environment.Exit(0);
        }

        private void About_Click(object? sender, EventArgs e)
        {
            Container.Resolve<IWindowService>().Show(new AboutView()
            {
                DataContext = Container.Resolve<AboutViewModel>()
            });
        }

        private void Preferences_Click(object? sender, EventArgs e)
        {
            Container.Resolve<IWindowService>().Show(new ApplicationSettingsView()
            {
                DataContext = Container.Resolve<ApplicationSettingsViewModel>()
            });
        }
    }
}