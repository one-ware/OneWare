using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Rendering;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using OneWare.Core.Data;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.ErrorList;
using OneWare.Output;
using OneWare.ProjectExplorer;
using OneWare.ProjectExplorer.Models;
using OneWare.SearchList;
using OneWare.SerialMonitor;
using OneWare.Settings;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
//using OneWare.Terminal;
using MessageBoxWindow = OneWare.Shared.Views.MessageBoxWindow;

namespace OneWare.Core
{
    public class App : PrismApplication
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            base.Initialize();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            //Services
            containerRegistry.RegisterSingleton<ILanguageManager, LanguageManager>();
            containerRegistry.RegisterSingleton<IActive, Active>();
            containerRegistry.RegisterSingleton<IDockService, DockService>();
            containerRegistry.RegisterSingleton<IWindowService, WindowService>();
            containerRegistry.RegisterSingleton<IModuleTracker, ModuleTracker>();
            containerRegistry.RegisterSingleton<BackupService>();

            //ViewModels - Windows
            containerRegistry.RegisterSingleton<MainWindowViewModel>();
            containerRegistry.RegisterSingleton<SettingsWindowViewModel>();

            //ViewModels - Dock
            containerRegistry.RegisterSingleton<WelcomeScreenViewModel>();
            containerRegistry.RegisterSingleton<MainDocumentDockViewModel>();
            
            containerRegistry.Register<EditViewModel>();

            //Windows
            containerRegistry.RegisterSingleton<MainWindow>();
        }

        protected override AvaloniaObject CreateShell()
        {
            //Register IDE Settings
            var settingsService = Container.Resolve<ISettingsService>();
            var paths = Container.Resolve<IPaths>();
            
            //General
            settingsService.RegisterSettingCategory("General", 0, "Material.ToggleSwitchOutline");

            //Editor settings
            settingsService.RegisterSettingCategory("Editor", 0, "BoxIcons.RegularCode");
            
            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_FontFamily", "Font", 
                "Editor Font Family", 
                "JetBrains Mono NL", 
                "JetBrains Mono NL", "Consolas", "Fira Code");
            
            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_FontSize", "Font Size",
                "Editor Font Size", 15, Enumerable.Range(10, 30).ToArray());

            // settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_SyntaxTheme", "Editor Theme", 
            //     "Setts the theme for Syntax Highlighting", ThemeName.DarkPlus, Enum.GetValues<ThemeName>());

            settingsService.RegisterTitled("Editor", "Formatting", "Editor_UseAutoFormatting", "Use Auto Formatting", "Use Auto Formatting in Editor", true);
            settingsService.RegisterTitled("Editor", "Formatting", "Editor_UseAutoBracket", "Use Auto Bracket", "Use Auto Bracket in Editor", true);

            settingsService.RegisterTitled("Editor", "Folding", "Editor_UseFolding", "Use Folding", "Use Folding in Editor", true);
            
            settingsService.RegisterTitled("Editor", "Backups", BackupService.KeyBackupServiceEnable, "Use Automatic Backups", "Use Automatic Backups in case the IDE crashes", true);
            settingsService.RegisterTitledCombo("Editor", "Backups", BackupService.KeyBackupServiceInterval, "Auto backup interval (s)", 
                "Interval the IDE uses to save files for backup", 30, 5, 10, 15, 30, 60, 120);
            
            settingsService.RegisterTitled("Editor", "External Changes", "Editor_DetectExternalChanges", "Detect external changes", "", true);
            settingsService.RegisterTitled("Editor", "External Changes", "Editor_NotifyExternalChanges", "Notify external changes", "", false);
            
            //TypeAssistance
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableHover", "Enable Hover Information", "Enable Hover Information", true);
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoCompletion", "Enable Code Suggestions", "Enable completion suggestions", true);
            settingsService.RegisterTitled("Editor", "Assistance", "TypeAssistance_EnableAutoFormatting", "Enable Auto Formatting", "Enable automatic formatting", true);
            
            settingsService.Load(Container.Resolve<IPaths>().SettingsPath);

            var windowService = Container.Resolve<IWindowService>();
            windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemViewModel()
            {
                Header = "Help",
                Priority = 1000
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemViewModel()
            {
                Header = $"About {paths.AppName}",
                Command = new RelayCommand(() => windowService.Show(new InfoWindow()
                {
                    DataContext = Container.Resolve<InfoWindowViewModel>()
                }))
            });
            var mainWindow = Container.Resolve<MainWindow>();

            //AvaloniaEdit Hyperlink support
            VisualLineLinkText.OpenUriEvent.AddClassHandler<Window>((window, args) =>
            {
                var link = args.Uri.ToString();
                Tools.OpenHyperLink(link);
            });

            mainWindow.Closing += (o, i) => _ = TryShutDownAsync(o, i);

            return mainWindow;
        }

        protected override IModuleCatalog CreateModuleCatalog()
        {
            return new AggregateModuleCatalog();
        }
        
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<SearchListModule>();
            moduleCatalog.AddModule<ErrorListModule>();
            //moduleCatalog.AddModule<TerminalModule>();
            moduleCatalog.AddModule<OutputModule>();
            moduleCatalog.AddModule<ProjectExplorerModule>();
            moduleCatalog.AddModule<SerialMonitorModule>();

            base.ConfigureModuleCatalog(moduleCatalog);
        }

        private async Task LoadContentAsync()
        {
            var arguments = Environment.GetCommandLineArgs();

            if (arguments.GetLength(0) > 1)
            {
                Global.SaveLastProjects = false;

                var fileName = arguments[1];
                //Check file exists
                if (File.Exists(fileName))
                {
                    if (string.Equals(Path.GetExtension(fileName), ".vhdpproj",
                        StringComparison.CurrentCultureIgnoreCase))
                    {
                        var r = await Container.Resolve<IProjectService>().LoadProjectAsync(fileName);

                        if (r is null) return;
                        
                        //Try open main file
                        if (r.Search(r.Header + ".vhdp") is ProjectFile mainFile)
                            _ = Container.Resolve<IDockService>().OpenFileAsync(mainFile);
                        else
                            //Open any file
                            foreach (var file in r.Items)
                                if (file is ProjectFile pf)
                                {
                                    _ = Container.Resolve<IDockService>().OpenFileAsync(pf);
                                    break;
                                }
                    }
                    else if (Path.GetExtension(fileName).StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    {
                        var file = Container.Resolve<IProjectService>().GetTemporaryFile(fileName);
                        _ = Container.Resolve<IDockService>().OpenFileAsync(file);
                    }
                    else
                    {
                        ContainerLocator.Container.Resolve<ILogger>()?.Log("Could not load file " + fileName);
                    }
                }
            }
            else
            {
                var key = Container.Resolve<IActive>().AddState("Loading last projects...", AppState.Loading);
                //await DockService.ProjectFiles.LoadLastProjectDataAsync();
                Container.Resolve<IActive>().RemoveState(key, "Projects loaded!");

                //Global.MainWindowViewModel.RefreshArduinoQuickMenu();
                //_ = Global.MainWindowViewModel.RefreshHardwareAsync();
                //_ = Global.MainWindowViewModel.RefreshSerialPortsAsync();
                //_ = Global.ArduinoBoardManagerViewModel.RefreshAsync();

                var dummy = new ProjectRoot(@"C:\Users\Hendrik\OneWareStudio\Projects\Test");
                dummy.AddFile("Test.vhd");
                
                Container.Resolve<IProjectService>().Items.Add(dummy);
                Container.Resolve<IProjectService>().ActiveProject = dummy;
                
                _ = FinishedLoadingAsync();
            }
        }

        private async Task FinishedLoadingAsync()
        {
            try
            {
                var settingsService = Container.Resolve<ISettingsService>();
                Container.Resolve<ILogger>()?.Log("Loading last projects finished!", ConsoleColor.Cyan);

                if (settingsService.GetSettingValue<string>("LastVersion") != Global.VersionCode)
                {
                    settingsService.SetSettingValue("LastVersion", Global.VersionCode);

                    Container.Resolve<IWindowService>().ShowNotificationWithButton("Update Successful!",
                        $"{Container.Resolve<IPaths>().AppName} got updated to {Global.VersionCode}!", "View Changelog", () =>
                        {
                            var clw = new ChangelogWindow();
                            clw.Show();
                        }, App.Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
                }

                await Task.Factory.StartNew(() =>
                {
                    //_ = Global.PackageManagerViewModel.CheckForUpdateAsync();
                }, new CancellationToken(), TaskCreationOptions.None, PriorityScheduler.BelowNormal);
            }
            catch (Exception e)
            {
                Container.Resolve<ILogger>().Error(e.Message, e);
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Container.Resolve<ISettingsService>().Load(Container.Resolve<IPaths>().SettingsPath);
            
            TypeAssistanceIconStore.Instance.Load();
            
            Container.Resolve<ILogger>().Log("Framework initialization complete!", ConsoleColor.Green);
            Container.Resolve<BackupService>().LoadAutoSaveFile();
            Container.Resolve<IDockService>().LoadLayout("Default");
            Container.Resolve<BackupService>().Init();
            
            Container.Resolve<ISettingsService>().GetSettingObservable<string>("Editor_FontFamily").Subscribe(x =>
            {
                if (x == null) return;
                
                if (Tools.IsFontInstalled(x))
                {
                    Resources["EditorFont"] = new FontFamily(x);
                    return;
                }
                var findFont = this.TryFindResource(x, out var resourceFont);
                if (findFont && resourceFont is FontFamily fFamily)
                { 
                    Resources["EditorFont"] = this.FindResource(x);
                    return;
                }
            });
            
            Container.Resolve<ISettingsService>().GetSettingObservable<int>("Editor_FontSize").Subscribe(x =>
            {
                Resources["EditorFontSize"] = x;
            });

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                async void Start()
                {
                    var splash = new SplashWindow()
                    {
                        DataContext = Container.Resolve<SplashWindowViewModel>()
                    };
                    splash.Show();
                    
                    await LoadContentAsync();

                    await Task.Delay(1000);
                    
                    splash?.Close();
                }
                
                Start();
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
            {
                throw new NotImplementedException();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task TryShutDownAsync(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;

            var unsavedFiles = new List<EditViewModel>();

            foreach (var tab in Container.Resolve<IDockService>().OpenFiles)
                if (tab.Value is EditViewModel {IsDirty: true} evm) unsavedFiles.Add(evm);

            var mainWin = this.MainWindow as Window;
            if (mainWin == null) throw new NullReferenceException(nameof(mainWin));
            var shutdownReady = await HandleUnsavedFilesAsync(unsavedFiles, mainWin);
            
            if (shutdownReady) await ShutdownAsync();
        }

        /// <summary>
        ///     Asks to save all files and returns true if ready to close or false if operation was canceled
        /// </summary>
        public static async Task<bool> HandleUnsavedFilesAsync(List<EditViewModel> unsavedFiles, Window dialogOwner)
        {
            if (unsavedFiles.Count > 0)
            {
                var msg = new MessageBoxWindow("Warning", "Keep unsaved changes?");

                await msg.ShowDialog(dialogOwner);

                if (msg.BoxStatus == MessageBoxStatus.Yes)
                {
                    for (var i = 0; i < unsavedFiles.Count; i++)
                        if (await unsavedFiles[i].SaveAsync())
                        {
                            unsavedFiles.RemoveAt(i);
                            i--;
                        }

                    if (unsavedFiles.Count == 0) return true;

                    var message = "Critical error saving some files: \n";
                    foreach (var file in unsavedFiles) message += file.Title + ", ";
                    message = message.Remove(message.Length - 2);
                    var mssg = new MessageBoxWindow("Error", message + "\nQuit anyways?", MessageBoxMode.NoCancel);
                    await mssg.ShowDialog(dialogOwner);

                    if (mssg.BoxStatus == MessageBoxStatus.Yes) return true;
                }
                else if (msg.BoxStatus == MessageBoxStatus.No)
                {
                    //Quit and discard changes
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private async Task ShutdownAsync()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime cds)
                foreach (var win in cds.Windows)
                    win.Hide();
            
            //DockService.ProjectFiles.SaveLastProjectData();
            Container.Resolve<BackupService>().CleanUp();

            await Container.Resolve<LanguageManager>().CleanResourcesAsync();

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
            Container.Resolve<IWindowService>().Show(Container.Resolve<InfoWindow>());
        }

        private void Preferences_Click(object? sender, EventArgs e)
        {
            Container.Resolve<IWindowService>().Show(Container.Resolve<SettingsWindow>());
        }
    }
}