using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.ErrorList.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.SDK.Enums;
using OneWare.SDK.Models;
using OneWare.SDK.Services;

namespace OneWare.ErrorList;

public class ErrorListModule : IModule
{
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IDockService _dockService;
    
    public const string KeyErrorListFilterMode = "ErrorList_FilterMode";
    public const string KeyErrorListShowExternalErrors = "ErrorList_ShowExternalErrors";
    public const string KeyErrorListVisibleSource = "ErrorList_VisibleSource";

    public ErrorListModule(ISettingsService settingsService, IWindowService windowService, IDockService dockService)
    {
        _settingsService = settingsService;
        _windowService = windowService;
        _dockService = dockService;
    }
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterManySingleton<ErrorListViewModel>(typeof(IErrorService),
            typeof(ErrorListViewModel));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        _dockService.RegisterLayoutExtension<IErrorService>(DockShowLocation.Bottom);
        
        _settingsService.Register(KeyErrorListFilterMode, 0);
        _settingsService.Register(KeyErrorListShowExternalErrors, true);
        _settingsService.Register(KeyErrorListVisibleSource, 0);
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemModel("Problems")
        {
            Header = "Problems",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<IErrorService>())),
            ImageIconObservable = Application.Current?.GetResourceObservable(ErrorListViewModel.IconKey) ,
        });
    }
}