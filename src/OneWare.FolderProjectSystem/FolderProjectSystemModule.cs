using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem.Models;

namespace OneWare.FolderProjectSystem;

public class FolderProjectSystemModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<FolderProjectManager>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        var manager = serviceProvider.Resolve<FolderProjectManager>();

        serviceProvider
            .Resolve<IProjectManagerService>()
            .RegisterProjectManager(FolderProjectRoot.ProjectType, manager);

        var welcomeScreenService = serviceProvider.Resolve<IWelcomeScreenService>();

        welcomeScreenService.RegisterItemToOpen("open_folder",
            new WelcomeScreenStartItem("open_folder", "Open folder...", new RelayCommand(() =>
                _ = serviceProvider.Resolve<IProjectExplorerService>().LoadProjectFolderDialogAsync(manager)))
            {
                Icon = new IconModel("VsImageLib.Folder16X")
            });

        serviceProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("Folder")
            {
                Header = "Folder",
                Command = new RelayCommand(() =>
                    _ = serviceProvider.Resolve<IProjectExplorerService>().LoadProjectFolderDialogAsync(manager)),
                Icon = new IconModel("VsImageLib.OpenFolder16X")
            });
    }
}