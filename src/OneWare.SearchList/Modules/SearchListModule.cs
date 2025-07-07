using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SearchList.ViewModels;


namespace OneWare.SearchList.Modules;
public class SearchListModule(IContainerAdapter containerAdapter) : IOneWareModule
{
    private readonly IContainerAdapter _containerAdapter = containerAdapter;    
    private IDockService? _dockService; // Removed readonly modifier
    private IWindowService? _windowService; // Removed readonly modifier

    public void RegisterTypes()
    {
        _dockService = _containerAdapter.Resolve<IDockService>();
        _windowService = _containerAdapter.Resolve<IWindowService>();
        
        _containerAdapter.Register<SearchListViewModel,SearchListViewModel>(isSingleton:true);

        OnExecute();
    }

    public void OnExecute()
    {
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Search")
        {
            Header = "Search",
            Command = new RelayCommand(() =>
            {
                var vm = _containerAdapter.Resolve<SearchListViewModel>();
                vm.SearchString = string.Empty;
                _dockService.Show(vm);
            }),
           // IconObservable = Application.Current!.GetResourceObservable(SearchListViewModel.IconKey),
            InputGesture = new KeyGesture(Key.F, KeyModifiers.Shift | PlatformHelper.ControlKey)
        });
    }
}