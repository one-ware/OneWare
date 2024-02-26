using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
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
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemViewModel("Extensions")
        {
            Header = "Extensions",
            Command = new RelayCommand(() => windowService.Show(new PackageManagerView()
            {
                DataContext = containerProvider.Resolve<PackageManagerViewModel>()
            })),
            IconObservable = Application.Current?.GetResourceObservable("PackageManager"),
        });
    }
}