using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SearchList.ViewModels;

namespace OneWare.SearchList;

public class SearchListModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<SearchListViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var windowService = serviceProvider.Resolve<IWindowService>();
        var dockService = serviceProvider.Resolve<IDockService>();

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows", new MenuItemViewModel("Search")
        {
            Header = "Search",
            Command = new RelayCommand(() =>
            {
                var vm = serviceProvider.Resolve<SearchListViewModel>();
                vm.SearchString = string.Empty;
                dockService.Show(vm);
            }),
            IconObservable = Application.Current!.GetResourceObservable(SearchListViewModel.IconKey),
            InputGesture = new KeyGesture(Key.F, KeyModifiers.Shift | PlatformHelper.ControlKey)
        });
    }
}
