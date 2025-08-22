using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using OneWare.ApplicationCommands.ViewModels;
using OneWare.ApplicationCommands.Views;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.Settings.Views;
using Prism.Ioc;
using UiExtension = OneWare.Essentials.Models.UiExtension;

namespace OneWare.Core.ViewModels.Windows;

public class MainWindowViewModel : ObservableObject
{
    private readonly IApplicationCommandService _applicationCommandService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    private IEditor? _currentEditor;
    private FlexibleWindow? _lastManagerWindow;

    private string _title;

    public MainWindowViewModel(IPaths paths, IApplicationStateService applicationStateService,
        IWindowService windowService, IDockService dockService,
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
        RightToolBarExtension = windowService.GetUiExtensions("MainWindow_RightToolBarExtension");
        BottomRightExtension = windowService.GetUiExtensions("MainWindow_BottomRightExtension");

        MainMenu = windowService.GetMenuItems("MainWindow_MainMenu");

        _title = paths.AppName;

        DockService.WhenValueChanged(x => x.CurrentDocument).Subscribe(x =>
        {
            if (x != null)
            {
                Title = $"{paths.AppName} - {Path.GetFileName(x.FullPath)}";

                CurrentEditor = x as IEditor;
            }
            else
            {
                Title = $"{paths.AppName}";
            }
        });

        _windowService.RegisterMenuItem("MainWindow_MainMenu/View", new MenuItemViewModel("FindAll")
        {
            Header = "Find All",
            Command = new RelayCommand(() => OpenManager(GetMainView(), "All")),
            InputGesture = new KeyGesture(Key.T, PlatformHelper.ControlKey)
        }, new MenuItemViewModel("FindActions")
        {
            Header = "Find Actions",
            Command = new RelayCommand(() => OpenManager(GetMainView(), "Actions")),
            InputGesture = new KeyGesture(Key.P, PlatformHelper.ControlKey | KeyModifiers.Shift)
        }, new MenuItemViewModel("FindFiles")
        {
            Header = "Find Files",
            Command = new RelayCommand(() => OpenManager(GetMainView(), "Files")),
            InputGesture = new KeyGesture(Key.A, PlatformHelper.ControlKey | KeyModifiers.Shift)
        });

        MainMenu.WatchTreeChanges(AddMenuItem, (r, p) => RemoveMenuItem(r));
    }

    public IDockService DockService { get; }
    public IApplicationStateService ApplicationStateService { get; }
    public IPaths Paths { get; }

    public ObservableCollection<MenuItemViewModel> TypeAssistanceQuickOptions { get; } = new();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private CompositeDisposable _currentEditorSubscriptionDisposable = new();
    
    public IEditor? CurrentEditor
    {
        get => _currentEditor;
        private set
        {
            _currentEditorSubscriptionDisposable.Dispose();
            _currentEditorSubscriptionDisposable = new CompositeDisposable();
            
            SetProperty(ref _currentEditor, value);

            TypeAssistanceQuickOptions.Clear();

            if (_currentEditor is EditViewModel evm)
            {
                evm.WhenValueChanged(x => x.TypeAssistance).Subscribe(x =>
                {
                    TypeAssistanceQuickOptions.Clear();
                    var quickOptions = (CurrentEditor as EditViewModel)?.TypeAssistance?.GetTypeAssistanceQuickOptions();
                    if (quickOptions != null) TypeAssistanceQuickOptions.AddRange(quickOptions);
                }).DisposeWith(_currentEditorSubscriptionDisposable);
            }
        }
    }

    public ObservableCollection<UiExtension> RoundToolBarExtension { get; }
    public ObservableCollection<UiExtension> LeftToolBarExtension { get; }
    
    public ObservableCollection<UiExtension> RightToolBarExtension { get; }
    public ObservableCollection<UiExtension> BottomRightExtension { get; }
    public ObservableCollection<MenuItemViewModel> MainMenu { get; }
    
    #region MainWindowButtons

    private Control GetMainView()
    {
        if (Application.Current!.ApplicationLifetime is ISingleViewApplicationLifetime isv)
            return isv.MainView ?? throw new NullReferenceException(nameof(isv.MainView));
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime icv)
            return icv.MainWindow ?? throw new NullReferenceException(nameof(icv.MainWindow));
        throw new Exception("MainView/MainWindow not found");
    }

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
            _lastManagerWindow = new CommandManagerView
            {
                DataContext = manager
            };
            _windowService.Show(_lastManagerWindow, logical as Window);
        }
    }

    private void AddMenuItem(MenuItemViewModel menuItem, string path = "")
    {
        if (menuItem.Command is not null)
            _applicationCommandService.RegisterCommand(new MenuItemApplicationCommand(menuItem, path));
    }

    private void RemoveMenuItem(MenuItemViewModel menuItem)
    {
        var removals = _applicationCommandService.ApplicationCommands.Where(x =>
            x is MenuItemApplicationCommand command && command.MenuItem == menuItem);
        _applicationCommandService.ApplicationCommands.RemoveMany(removals);
    }

    public Task OpenSettingsDialogAsync()
    {
        return _windowService.ShowDialogAsync(new ApplicationSettingsView
        {
            DataContext = ContainerLocator.Container.Resolve<ApplicationSettingsViewModel>()
        });
    }

    #endregion
}