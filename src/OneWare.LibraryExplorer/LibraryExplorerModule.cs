using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.LibraryExplorer.ViewModels;
using OneWare.ProjectExplorer.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.LibraryExplorer;

public class LibraryExplorerModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<LibraryExplorerViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var dockService = containerProvider.Resolve<IDockService>();
        var windowService = containerProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<LibraryExplorerViewModel>(DockShowLocation.Left);
        
        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Library Explorer")
            {
                Header = "Library Explorer",
                Command =
                    new RelayCommand(() => dockService.Show(containerProvider.Resolve<LibraryExplorerViewModel>())),
                IconObservable = Application.Current!.GetResourceObservable(LibraryExplorerViewModel.IconKey)
            });
    }
}