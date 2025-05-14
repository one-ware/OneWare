using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem.Models;
using Autofac; // Add this for Autofac

namespace OneWare.FolderProjectSystem;

public class FolderProjectSystemModule
{
    private readonly ILifetimeScope _container;

    // Constructor now accepts ILifetimeScope to resolve dependencies
    public FolderProjectSystemModule(ILifetimeScope container)
    {
        _container = container;
    }

    public void RegisterTypes()
    {
        // Register types in the container as needed
        // Example:
        // _container.RegisterType<FolderProjectManager>().AsSelf();
    }

    public void OnInitialized()
    {
        // Resolving the FolderProjectManager from Autofac container
        var manager = _container.Resolve<FolderProjectManager>();

        _container.Resolve<IProjectManagerService>()
                  .RegisterProjectManager(FolderProjectRoot.ProjectType, manager);

        // Resolving IWindowService to register the menu item
        _container.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemViewModel("Folder")
            {
                Header = "Folder",
                Command = new RelayCommand(() =>
                    _ = _container.Resolve<IProjectExplorerService>().LoadProjectFolderDialogAsync(manager)),
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.OpenFolder16X")
            });
    }
}
