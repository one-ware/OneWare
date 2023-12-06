using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using OneWare.ApplicationCommands.Models;
using OneWare.ApplicationCommands.ViewModels;
using OneWare.ApplicationCommands.Views;
using OneWare.Core.Models;
using OneWare.Core.ViewModels.DockViews;
using OneWare.SDK.Controls;
using OneWare.SDK.Extensions;
using OneWare.SDK.Helpers;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using Prism.Ioc;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.Core.ViewModels.Windows
{
    public class MainWindowViewModel : ObservableObject
    {
        private readonly IApplicationCommandService _applicationCommandService;
        private readonly ISettingsService _settingsService;
        private readonly IWindowService _windowService;
        private FlexibleWindow? _lastManagerWindow;

        public IDockService DockService { get; }
        public IApplicationStateService ApplicationStateService { get; }
        public IPaths Paths { get; }
        
        public ObservableCollection<MenuItemViewModel> TypeAssistanceQuickOptions { get; } = new();
        
        private string _title;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private IEditor? _currentEditor;
        public IEditor? CurrentEditor
        {
            get => _currentEditor;
            private set => SetProperty(ref _currentEditor, value);
        }
        
        public ObservableCollection<Control> RoundToolBarExtension { get; }
        public ObservableCollection<Control> LeftToolBarExtension { get; }
        public ObservableCollection<Control> BottomRightExtension { get; }
        public ObservableCollection<MenuItemViewModel> MainMenu { get; }
        
        public MainWindowViewModel(IPaths paths, IApplicationStateService applicationStateService, IWindowService windowService, IDockService dockService,
            ISettingsService settingsService, IApplicationCommandService applicationCommandService)
        {
            _applicationCommandService = applicationCommandService;
            ApplicationStateService = applicationStateService;
            _windowService = windowService;
            DockService = dockService;
            Paths = paths;
            _settingsService = settingsService;
            
            RoundToolBarExtension = windowService.GetUiExtensions("MainWindow_RoundToolBarExtension");
            LeftToolBarExtension = windowService.GetUiExtensions("MainWindow_LeftToolBarExtension");
            BottomRightExtension = windowService.GetUiExtensions("MainWindow_BottomRightExtension");

            MainMenu = windowService.GetMenuItems("MainWindow_MainMenu");

            _title = paths.AppName;
            
            DockService.WhenValueChanged(x => x.CurrentDocument).Subscribe(x =>
            {
                if (x != null)
                {
                    Title = $"{paths.AppName} - {x.CurrentFile?.Header}";
                    
                    if (x is IEditor editor)
                    {
                        CurrentEditor = editor;
                        TypeAssistanceQuickOptions.Clear();
                        var quickOptions = (CurrentEditor as EditViewModel)?.TypeAssistance?.GetTypeAssistanceQuickOptions();
                        if(quickOptions != null) TypeAssistanceQuickOptions.AddRange(quickOptions);
                    }
                }
                else
                {
                    Title = $"{paths.AppName}";
                }
            });
            
            applicationCommandService.RegisterCommand(new LogicalApplicationCommand<ILogical>("Open Actions", x => OpenManager(x, "Actions"), new KeyGesture(Key.Q, PlatformHelper.ControlKey)));
            applicationCommandService.RegisterCommand(new LogicalApplicationCommand<ILogical>("Open Files", x => OpenManager(x, "Files"), new KeyGesture(Key.T, PlatformHelper.ControlKey)));
            
            MainMenu.WatchTreeChanges(AddMenuItem, (r,p) => RemoveMenuItem(r));
        }

        #region MainWindowButtons
        
        private void OpenManager(ILogical logical, string startTab)
        {
            if (_lastManagerWindow?.IsAttachedToVisualTree() ?? false)
            {
                var manager = _lastManagerWindow.DataContext as CommandManagerViewModel;
                if (manager == null) throw new NullReferenceException(nameof(manager));
                manager.SelectedTab = manager.Tabs.First(t => t.Title == startTab);
            }
            else
            {
                var manager = ContainerLocator.Container.Resolve<CommandManagerViewModel>((typeof(ILogical), logical));
                manager.SelectedTab = manager.Tabs.First(t => t.Title == startTab);
                _lastManagerWindow = new CommandManagerView()
                {
                    DataContext = manager
                };
                _windowService.Show(_lastManagerWindow, logical as Window);
            }
        }
        
        private void AddMenuItem(MenuItemViewModel menuItem, string path = "")
        {
            if(menuItem.Command is not null) 
                _applicationCommandService.RegisterCommand(new MenuItemApplicationCommand(menuItem, path));
        }
        
        private void RemoveMenuItem(MenuItemViewModel menuItem)
        {
            var removals = _applicationCommandService.ApplicationCommands.Where(x =>
                x is MenuItemApplicationCommand command && command.MenuItem == menuItem);
            _applicationCommandService.ApplicationCommands.RemoveMany(removals);
        }

        public void OpenSettings()
        {
            _windowService.Show(new ApplicationSettingsView()
            {
                DataContext = ContainerLocator.Container.Resolve<ApplicationSettingsViewModel>()
            });
        }
        
        #endregion
    }
}