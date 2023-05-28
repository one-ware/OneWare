using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.SearchList.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.SearchList;

public class SearchListModule : IModule
{
    private readonly IWindowService _windowService;
    private readonly IDockService _dockService;
    
    public SearchListModule(IWindowService windowService, IDockService dockService)
    {
        _windowService = windowService;
        _dockService = dockService;
    }
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<SearchListViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<ISettingsService>().Register("SearchList_FilterMode", 0);
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel()
        {
            Header = "Search",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<SearchListViewModel>())),
            Icon = Application.Current?.FindResource("BoxIcons.RegularCode") as IImage,
        });
    }
}