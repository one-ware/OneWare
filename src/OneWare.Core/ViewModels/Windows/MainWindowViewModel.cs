using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using DynamicData.Binding;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using OneWare.Shared;
using Prism.Ioc;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using ObservableExtensions = System.ObservableExtensions;

namespace OneWare.Core.ViewModels.Windows
{
    public class MainWindowViewModel : ViewModelBase
    {
        public IDockService DockService { get; }
        public IActive Active { get; }
        public IWindowService WindowService { get; }
        
        public IPaths Paths { get; }

        private readonly ISettingsService _settingsService;

        public ObservableCollection<MenuItemViewModel> TypeAssistanceQuickOptions { get; } = new();
        
        private string _title;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private EditViewModel? _currentEditor;
        public EditViewModel? CurrentEditor
        {
            get => _currentEditor;
            set => SetProperty(ref _currentEditor, value);
        }
        
        public ObservableCollection<Control> RoundToolBarIcons { get; }
        
        public ObservableCollection<MenuItemViewModel> MainMenu { get; }

        public MainWindowViewModel(IPaths paths, IActive active, IWindowService windowService, IDockService dockService,
            ISettingsService settingsService)
        {
            Active = active;
            WindowService = windowService;
            DockService = dockService;
            Paths = paths;
            _settingsService = settingsService;
            
            RoundToolBarIcons = windowService.GetUiExtensions("MainWindow_RoundToolBar");
            MainMenu = windowService.GetMenuItems("MainWindow_MainMenu");

            _title = paths.AppName;
            
            ObservableExtensions.Subscribe(DockService.WhenValueChanged(x => x.CurrentDocument), x =>
            {
                if (x is EditViewModel evm)
                {
                    CurrentEditor = evm;
                    Title = $"{paths.AppName} IDE {evm.CurrentFile.Header}";
                }
                else
                {
                    Title = $"{paths.AppName} IDE";
                }
            });
        }

        #region MainWindowButtons

        /// <summary>
        ///     Saves open files
        /// </summary>
        public void Save(bool all)
        {
            if (all)
                foreach (var file in DockService.OpenFiles)
                {
                    _ = file.Value.SaveAsync();
                }
            else
                _ = DockService.CurrentDocument?.SaveAsync();
        }

        public void OpenSettings()
        {
            var settings = ContainerLocator.Container.Resolve<SettingsWindow>();
            settings.DataContext = ContainerLocator.Container.Resolve<SettingsWindowViewModel>();
            WindowService.Show(settings);
        }
        
        #endregion
    }
}