using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.SearchList.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

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
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Search")
        {
            Header = "Search",
            Command = new RelayCommand(() =>
            {
                var vm = containerProvider.Resolve<SearchListViewModel>();
                vm.SearchString = string.Empty;
                _dockService.Show(vm);
            }),
            IconObservable = Application.Current!.GetResourceObservable(SearchListViewModel.IconKey),
            InputGesture = new KeyGesture(Key.F, KeyModifiers.Shift | PlatformHelper.ControlKey)
        });
    }
}