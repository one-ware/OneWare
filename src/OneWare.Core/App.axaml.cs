using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using CommunityToolkit.Mvvm.Input;
using OneWare.Core.Data;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.ErrorList;
using OneWare.FolderProjectSystem.Models;
using OneWare.Output;
using OneWare.ProjectExplorer;
using OneWare.ProjectSystem.Models;
using OneWare.ProjectSystem.Services;
using OneWare.SearchList;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

//using OneWare.Terminal

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
            containerRegistry.RegisterSingleton<IHttpService, HttpService>();
            containerRegistry.RegisterSingleton<IPackageService, PackageService>();
            containerRegistry.RegisterSingleton<IProjectManagerService, ProjectManagerService>();
            containerRegistry.RegisterSingleton<ILanguageManager, LanguageManager>();
            containerRegistry.RegisterSingleton<IActive, Active>();
            containerRegistry.RegisterSingleton<IDockService, DockService>();
            containerRegistry.RegisterSingleton<IWindowService, WindowService>();
            containerRegistry.RegisterSingleton<IModuleTracker, ModuleTracker>();
            containerRegistry.RegisterSingleton<BackupService>();

            //ViewModels - Windows
            containerRegistry.RegisterSingleton<MainWindowViewModel>();
            containerRegistry.RegisterSingleton<SettingsViewModel>();
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
            
            //General
            settingsService.RegisterSettingCategory("General", 0, "Material.ToggleSwitchOutline");

            //Editor settings
            settingsService.RegisterSettingCategory("Editor", 0, "BoxIcons.RegularCode");
            
            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_FontFamily", "Font", 
                "Editor Font Family", 
                "JetBrains Mono NL", 
                "JetBrains Mono NL", "IntelOne Mono", "Consolas", "Fira Code");
            
            settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_FontSize", "Font Size",
                "Editor Font Size", 15, Enumerable.Range(10, 30).ToArray());

            // settingsService.RegisterTitledCombo("Editor", "Appearance", "Editor_SyntaxTheme", "Editor Theme", 
            //     "Setts the theme for Syntax Highlighting", ThemeName.DarkPlus, Enum.GetValues<ThemeName>());

            settingsService.RegisterTitled("Editor", "Formatting", "Editor_UseAutoFormatting", "Use Auto Formatting", "Use Auto Formatting in Editor", true);
            settingsService.RegisterTitled("Editor", "Formatting", "Editor_UseAutoBracket", "Use Auto Bracket", "Use Auto Bracket in Editor", true);

            settingsService.RegisterTitled("Editor", "Folding", "Editor_UseFolding", "Use Folding", "Use Folding in Editor", true);
            
            settingsService.RegisterTitled("Editor", "Backups", BackupService.KeyBackupServiceEnable, "Use Automatic Backups", "Use Automatic Backups in case the IDE crashes", ApplicationLifetime is IClassicDesktopStyleApplicationLifetime);
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
            windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("Help")
            {
                Header = "Help",
                Priority = 1000
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemModel("Changelog")
            {
                Header = $"Changelog",
                Command = new RelayCommand(() => windowService.Show(new ChangelogView()
                {
                    DataContext = Container.Resolve<ChangelogViewModel>()
                }))
            });
            windowService.RegisterMenuItem("MainWindow_MainMenu/Help", new MenuItemModel("About")
            {
                Header = $"About {paths.AppName}",
                Command = new RelayCommand(() => windowService.Show(new AboutView()
                {
                    DataContext = Container.Resolve<AboutViewModel>()
                }))
            });
            
            //AvaloniaEdit Hyperlink support
            VisualLineLinkText.OpenUriEvent.AddClassHandler<Window>((window, args) =>
            {
                var link = args.Uri.ToString();
                Tools.OpenHyperLink(link);
            });

            if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                var mainView = Container.Resolve<MainView>();
                mainView.DataContext = DataContext = ContainerLocator.Container.Resolve<MainWindowViewModel>();
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
            return new AggregateModuleCatalog();
        }
        
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<SearchListModule>();
            moduleCatalog.AddModule<ErrorListModule>();
            moduleCatalog.AddModule<OutputModule>();
            moduleCatalog.AddModule<ProjectExplorerModule>();

            base.ConfigureModuleCatalog(moduleCatalog);
        }

        protected virtual string GetDefaultLayoutName => "Default";
        
        public override void OnFrameworkInitializationCompleted()
        {
            Container.Resolve<ISettingsService>().Load(Container.Resolve<IPaths>().SettingsPath);

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
                if (Tools.IsFontInstalled(x))
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
                
        private async Task LoadContentAsync()
        {
            Window? splashWindow = null; 
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                splashWindow = new SplashWindow()
                {
                    DataContext = Container.Resolve<SplashWindowViewModel>()
                };
                splashWindow.Show();
            }
            
            var arguments = Environment.GetCommandLineArgs();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime && arguments.GetLength(0) > 1)
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
                        ContainerLocator.Container.Resolve<ILogger>()?.Log("Could not load file " + fileName);
                    }
                }
            }
            else
            {
                var key = Container.Resolve<IActive>().AddState("Loading last projects...", AppState.Loading);
                //await DockService.ProjectFiles.LoadLastProjectDataAsync();

                //Global.MainWindowViewModel.RefreshArduinoQuickMenu();
                //_ = Global.MainWindowViewModel.RefreshHardwareAsync();
                //_ = Global.MainWindowViewModel.RefreshSerialPortsAsync();
                //_ = Global.ArduinoBoardManagerViewModel.RefreshAsync();

                var testProj = Path.Combine(Container.Resolve<IPaths>().ProjectsDirectory, "Test");
                Directory.CreateDirectory(testProj);
                var dummy = new FolderProjectRoot(testProj);
                var hard = dummy.AddFile("Hardware.vhd");
                var soft = dummy.AddFile("Software.cpp");
                Container.Resolve<IProjectExplorerService>().Items.Add(dummy);
                Container.Resolve<IProjectExplorerService>().ActiveProject = dummy;
                
                Container.Resolve<IDockService>().InitializeDocuments();
                Container.Resolve<IActive>().RemoveState(key, "Projects loaded!");


                if (ApplicationLifetime is ISingleViewApplicationLifetime)
                {
                    var editor = await Container.Resolve<IDockService>().OpenFileAsync(soft);
                    (editor as IEditor)!.CurrentDocument.Text = @"
// Your First C++ Program

#include <iostream>

int main() 
{
    std::cout << 'Hello World!';
    return 0;
}
";
                    editor = await Container.Resolve<IDockService>().OpenFileAsync(hard);
                    (editor as IEditor)!.CurrentDocument.Text = @"
  
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.numeric_std.all; 

      
ENTITY VHDPlus IS
PORT (
  --#IOVoltagePins
  CLK : IN STD_LOGIC;
  
  led: OUT STD_LOGIC := '0'

);
END VHDPlus;

ARCHITECTURE BEHAVIORAL OF VHDPlus IS
  
BEGIN

  --#SetIOVoltage
  PROCESS (CLK)  
    VARIABLE Thread3 : NATURAL range 0 to 6000004 := 0;
  BEGIN
  IF RISING_EDGE(CLK) THEN
    CASE (Thread3) IS
      WHEN 0 =>
        led <= '0';
        Thread3 := 1;
      WHEN 1 to 3000001 =>
        Thread3 := Thread3 + 1;
      WHEN 3000002 =>
        led <= '1';
        Thread3 := 3000003;
      WHEN 3000003 to 6000003 =>
        IF (Thread3 < 6000003) THEN 
          Thread3 := Thread3 + 1;
        ELSE
          Thread3 := 0;
        END IF;
      WHEN others => Thread3 := 0;
    END CASE;
  END IF;
  END PROCESS;
  
END BEHAVIORAL;
";
                }

                _ = FinishedLoadingAsync();
            }
            
            await Task.Delay(1000);
            splashWindow?.Close();
        }

        private Task FinishedLoadingAsync()
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
                            Container.Resolve<IWindowService>().Show(new ChangelogView());
                        }, App.Current?.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage);
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

            return Task.CompletedTask;
        }

        private async Task TryShutDownAsync(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;

            var unsavedFiles = new List<IExtendedDocument>();

            foreach (var tab in Container.Resolve<IDockService>().OpenFiles)
                if (tab.Value is {IsDirty: true} evm) unsavedFiles.Add(evm);

            var mainWin = MainWindow as Window;
            if (mainWin == null) throw new NullReferenceException(nameof(mainWin));
            var shutdownReady = await Tools.HandleUnsavedFilesAsync(unsavedFiles, mainWin);
            
            if (shutdownReady) await ShutdownAsync();
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
            Container.Resolve<IWindowService>().Show(Container.Resolve<AboutView>());
        }

        private void Preferences_Click(object? sender, EventArgs e)
        {
            Container.Resolve<IWindowService>().Show(Container.Resolve<SettingsView>());
        }
    }
}