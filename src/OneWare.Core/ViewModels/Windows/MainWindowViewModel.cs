using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Core.Models;
using OneWare.Core.ViewModels.DockViews;
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
        public IDockService DockService { get; }
        public IActive Active { get; }
        public IWindowService WindowService { get; }
        public IPaths Paths { get; }

        private readonly ISettingsService _settingsService;
        
        public ObservableCollection<IMenuItem> TypeAssistanceQuickOptions { get; } = new();
        
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
            set => SetProperty(ref _currentEditor, value);
        }
        
        public ObservableCollection<Control> RoundToolBarExtension { get; }
        public ObservableCollection<Control> LeftToolBarExtension { get; }
        public ObservableCollection<Control> BottomRightExtension { get; }
        public ObservableCollection<IMenuItem> MainMenu { get; }
        
        public MainWindowViewModel(IPaths paths, IActive active, IWindowService windowService, IDockService dockService,
            ISettingsService settingsService)
        {
            Active = active;
            WindowService = windowService;
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
                _ = (DockService.Layout?.FocusedDockable as IExtendedDocument)?.SaveAsync();
        }

        public void OpenSettings()
        {
            WindowService.Show(new ApplicationSettingsView()
            {
                DataContext = ContainerLocator.Container.Resolve<ApplicationSettingsViewModel>()
            });
        }
        
        #endregion
    }
}