using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalProjectSystemModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var manager = containerProvider.Resolve<UniversalFpgaProjectManager>();
        
        containerProvider
            .Resolve<IProjectManagerService>()
            .RegisterProjectManager(UniversalFpgaProjectRoot.ProjectType, manager);

        containerProvider.Resolve<IWindowService>().RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemModel("FpgaProject")
            {
                Header = "Project",
                Command = new RelayCommand(() => _ = containerProvider.Resolve<IProjectExplorerService>().LoadProjectFolderDialogAsync(manager)),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.OpenFolder16X")
            });
    }
}