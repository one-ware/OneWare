using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.LibraryExplorer.ViewModels;

namespace OneWare.LibraryExplorer;

public class LibraryExplorerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<LibraryExplorerViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var dockService = serviceProvider.Resolve<IMainDockService>();
        var windowService = serviceProvider.Resolve<IWindowService>();

        dockService.RegisterLayoutExtension<LibraryExplorerViewModel>(DockShowLocation.Left);

        windowService.RegisterMenuItem("MainWindow_MainMenu/View/Tool Windows",
            new MenuItemModel("Library Explorer")
            {
                Header = "Library Explorer",
                Command = new RelayCommand(() => dockService.Show(serviceProvider.Resolve<LibraryExplorerViewModel>())),
                Icon = new IconModel(LibraryExplorerViewModel.IconKey)
            });
    }
}
