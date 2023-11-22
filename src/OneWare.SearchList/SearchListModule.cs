using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using OneWare.SearchList.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.SDK.Models;
using OneWare.SDK.Services;

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
        var hotkey = new KeyGesture(Key.F, KeyModifiers.Control | KeyModifiers.Shift);
        
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemModel("Search")
        {
            Header = "Search",
            Command = new RelayCommand(() => _dockService.Show(containerProvider.Resolve<SearchListViewModel>())),
            ImageIconObservable = Application.Current?.GetResourceObservable(SearchListViewModel.IconKey),
            InputGesture = hotkey,
        });
    }
}