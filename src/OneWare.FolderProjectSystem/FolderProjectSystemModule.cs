using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.FolderProjectSystem.Models;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
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
            new MenuItemModel("Folder")
            {
                Header = "Folder",
                Command = new RelayCommand(() => _ = containerProvider.Resolve<IProjectExplorerService>().LoadProjectFolderDialogAsync(manager)),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.OpenFolder16X")
            });
    }
}