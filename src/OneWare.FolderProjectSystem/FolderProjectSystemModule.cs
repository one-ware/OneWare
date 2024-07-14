using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem.Models;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.FolderProjectSystem;

public class FolderProjectSystemModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var manager = containerProvider.Resolve<FolderProjectManager>();

        containerProvider
            .Resolve<IProjectManagerService>()
            .RegisterProjectManager(FolderProjectRoot.ProjectType, manager);

        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemViewModel("Folder")
            {
                Header = "Folder",
                Command = new RelayCommand(() =>
                    _ = containerProvider.Resolve<IProjectExplorerService>().LoadProjectFolderDialogAsync(manager)),
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.OpenFolder16X")
            });
    }
}