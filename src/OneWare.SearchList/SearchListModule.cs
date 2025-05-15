using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SearchList.ViewModels;
using Autofac;

namespace OneWare.SearchList;

public class SearchListModule
{
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;

    // Constructor with dependency injection via Autofac
    public SearchListModule(IWindowService windowService, IDockService dockService)
    {
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
    }

    // Register types with Autofac
    public void RegisterTypes(ContainerBuilder builder)
    {
        builder.RegisterType<SearchListViewModel>().AsSelf().SingleInstance();
    }

    // Initialization logic using Autofac
    public void OnInitialized(IComponentContext container)
    {
        _windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Search")
        {
            Header = "Search",
            Command = new RelayCommand(() =>
            {
                var vm = container.Resolve<SearchListViewModel>();  // Resolving SearchListViewModel via Autofac
                vm.SearchString = string.Empty;
                _dockService.Show(vm);
            }),
            IconObservable = Application.Current!.GetResourceObservable(SearchListViewModel.IconKey),
            InputGesture = new KeyGesture(Key.F, KeyModifiers.Shift | PlatformHelper.ControlKey)
        });
    }
}
