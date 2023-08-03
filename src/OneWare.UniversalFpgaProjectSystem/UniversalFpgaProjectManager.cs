using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Shared;
using OneWare.Shared.Helpers;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectManager : IProjectManager
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    
    public UniversalFpgaProjectManager(IProjectExplorerService projectExplorerService, IDockService dockService, IWindowService windowService)
    {
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _windowService = windowService;
    }

    public async Task NewProjectDialogAsync()
    {
        await _windowService.ShowDialogAsync(new UniversalFpgaProjectCreatorView()
        {
            DataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCreatorViewModel>()
        });
    }
    
    public async Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        var root = await UniversalFpgaProjectParser.DeserializeAsync(path);
        
        if(root != null)
            ProjectHelpers.ImportEntries(root.FullPath, root);
        
        return root;
    }

    public async Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return root is UniversalFpgaProjectRoot uFpga && await UniversalFpgaProjectParser.SerializeAsync(uFpga);
    }

    public IEnumerable<MenuItemModel> ConstructContextMenu(IProjectEntry entry)
    {
        switch (entry)
        {
            case UniversalFpgaProjectRoot root:
                yield return new MenuItemModel("Save")
                {
                    Header = "Save",
                    Command = new AsyncRelayCommand(() => SaveProjectAsync(root)),
                    ImageIconObservable = Application.Current!.GetResourceObservable("VsImageLib.Save16XMd"),
                };
                yield return new MenuItemModel("Reload")
                {
                    Header = $"Reload",
                    Command = new AsyncRelayCommand(() => _projectExplorerService.ReloadAsync(root)),
                    ImageIconObservable = Application.Current!.GetResourceObservable("VsImageLib.RefreshGrey16X"),
                };
                yield return new MenuItemModel("Edit")
                {
                    Header = $"Edit {Path.GetFileName(root.ProjectFilePath)}",
                    Command = new AsyncRelayCommand(() => _dockService.OpenFileAsync(_projectExplorerService.GetTemporaryFile(root.ProjectFilePath))),
                };
                break;
        }
    }
}