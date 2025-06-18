using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.LibraryExplorer.ViewModels;
using OneWare.ProjectExplorer.ViewModels;
using Prism.Ioc;
using Prism.Modularity; // Still had Prism references

namespace OneWare.LibraryExplorer;

public class LibraryExplorerModule // Was not inheriting anything, but was used with IContainerRegistry/IContainerProvider
{
    // Constructor existed, but OnInitialized used containerProvider.Resolve
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    private readonly LibraryExplorerViewModel _libraryExplorerViewModel; // Assumed injected in previous step

    public LibraryExplorerModule(IDockService dockService, IWindowService windowService, LibraryExplorerViewModel libraryExplorerViewModel)
    {
        _dockService = dockService;
        _windowService = windowService;
        _libraryExplorerViewModel = libraryExplorerViewModel;
    }

    public void RegisterTypes(IContainerRegistry containerRegistry) // Prism method
    {
        containerRegistry.RegisterSingleton<LibraryExplorerViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider) // Prism method
    {
        var dockService = containerProvider.Resolve<IDockService>(); // Service Locator
        var windowService = containerProvider.Resolve<IWindowService>(); // Service Locator

        dockService.RegisterLayoutExtension<LibraryExplorerViewModel>(DockShowLocation.Left);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemViewModel("Library Explorer")
            {
                Header = "Library Explorer",
                Command = new RelayCommand(() => dockService.Show(containerProvider.Resolve<LibraryExplorerViewModel>())), // Service Locator
                IconObservable = Application.Current!.GetResourceObservable(LibraryExplorerViewModel.IconKey)
            });
    }
}