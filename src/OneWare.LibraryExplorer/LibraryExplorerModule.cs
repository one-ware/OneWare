using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.LibraryExplorer.ViewModels;
using OneWare.ProjectExplorer.ViewModels;
using Microsoft.Extensions.Logging;
using Autofac;

namespace OneWare.LibraryExplorer;

public class LibraryExplorerModule 
{
    private readonly ILogger<LibraryExplorerModule> _logger;

    public LibraryExplorerModule(ILogger<LibraryExplorerModule> logger)
    {
        _logger = logger;
    }

    public void RegisterTypes(ContainerBuilder containerBuilder)
    {
        containerBuilder.RegisterType<LibraryExplorerViewModel>().AsSelf().SingleInstance();
    }

    public void OnInitialized(IComponentContext container)
    {
        try
        {
            var dockService = container.Resolve<IDockService>();
            var windowService = container.Resolve<IWindowService>();

            dockService.RegisterLayoutExtension<LibraryExplorerViewModel>(DockShowLocation.Left);

            windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
                new MenuItemViewModel("Library Explorer")
                {
                    Header = "Library Explorer",
                    Command = new RelayCommand(() => dockService.Show(container.Resolve<LibraryExplorerViewModel>())),
                    IconObservable = Application.Current!.GetResourceObservable(LibraryExplorerViewModel.IconKey)
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initialization of LibraryExplorerModule.");
        }
    }
}
