using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.PackageManager;

public class PackageManagerModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<PackageManagerViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var windowService = containerProvider.Resolve<IWindowService>();
        
        windowService.RegisterMenuItem("MainWindow_MainMenu", new MenuItemModel("Extras")
        {
            Header = "Extras",
            Priority = 900
        });
        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemModel("Extensions")
        {
            Header = "Extensions",
            Command = new RelayCommand(() => windowService.Show(new PackageManagerView()
            {
                DataContext = containerProvider.Resolve<PackageManagerViewModel>()
            })),
            ImageIconObservable = Application.Current?.GetResourceObservable("PackageManager"),
        });
    }
}