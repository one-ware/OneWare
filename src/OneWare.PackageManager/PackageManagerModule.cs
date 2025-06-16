using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;
using Prism.Modularity;

namespace OneWare.PackageManager;

public class PackageManagerModule 
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IPackageService, PackageService>();
        containerRegistry.RegisterSingleton<PackageManagerViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var windowService = containerProvider.Resolve<IWindowService>();

        windowService.RegisterMenuItem("MainWindow_MainMenu/Extras", new MenuItemViewModel("Extensions")
        {
            Header = "Extensions",
            Command = new RelayCommand(() => windowService.Show(new PackageManagerView
            {
                DataContext = containerProvider.Resolve<PackageManagerViewModel>()
            })),
            IconObservable = Application.Current!.GetResourceObservable("PackageManager")
        });
        
        ContainerLocator.Container.Resolve<ISettingsService>().RegisterSettingCategory("Package Manager", 0, "PackageManager");
        
        ContainerLocator.Container.Resolve<ISettingsService>()
            .RegisterSetting("Package Manager", "Sources", "PackageManager_Sources",  new ListBoxSetting("Custom Package Sources", [])
            {
                MarkdownDocumentation = """
                                        Add custom package sources to the package manager. These sources will be used to search for and install packages.
                                        You can add either:
                                        - A Package Repository
                                        - A Direct link to a package manifest
                                        """,
            });
    }
}