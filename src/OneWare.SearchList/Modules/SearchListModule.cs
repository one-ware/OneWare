using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SearchList.ViewModels;


namespace OneWare.SearchList.Modules;
public class SearchListModule
{
    private readonly IContainerAdapter _containerAdapter;    
    private IDockService? _dockService; // Removed readonly modifier
    private IWindowService? _windowService; // Removed readonly modifier

    public SearchListModule(IContainerAdapter containerAdapter)
    {
        _containerAdapter = containerAdapter;
    }

    public void Load()
    {
        _dockService = _containerAdapter.Resolve<IDockService>();
        _windowService = _containerAdapter.Resolve<IWindowService>();
        
        _containerAdapter.Register<SearchListViewModel,SearchListViewModel>(isSingleton:true);

        Register();
    }

    private void Register()
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
            IconObservable = Application.Current!.GetResourceObservable(SearchListViewModel.IconKey),
            InputGesture = new KeyGesture(Key.F, KeyModifiers.Shift | PlatformHelper.ControlKey)
        });
    }
}