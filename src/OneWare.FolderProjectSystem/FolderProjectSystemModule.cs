using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.FolderProjectSystem.Models;
using Prism.Modularity;

namespace OneWare.FolderProjectSystem;

public class FolderProjectSystemModule 
{
    private readonly FolderProjectManager _folderProjectManager;
    private readonly IProjectManagerService _projectManagerService;
    private readonly IWindowService _windowService;
    private readonly IProjectExplorerService _projectExplorerService;

    public FolderProjectSystemModule(FolderProjectManager folderProjectManager, 
                                     IProjectManagerService projectManagerService ,
                                     IProjectExplorerService projectExplorerService,
                                      IWindowService windowService)
    {

        _folderProjectManager = folderProjectManager;
        _projectManagerService = projectManagerService;
        _windowService = windowService;
        _projectExplorerService = projectExplorerService;
    }


    public void OnInitialized()
    {
        
        _projectManagerService.RegisterProjectManager(FolderProjectRoot.ProjectType, manager);

        _windowService.RegisterMenuItem("MainWindow_MainMenu/File/Open",
            new MenuItemViewModel("Folder")
            {
                Header = "Folder",
                Command = new RelayCommand(() =>
                    _ = _projectExplorerService.LoadProjectFolderDialogAsync(manager)),
                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.OpenFolder16X")
            });
    }
}