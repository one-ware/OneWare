using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;
using OneWare.SoftwareProjectSystem.Models;
using OneWare.SoftwareProjectSystem.ViewModels;
using OneWare.SoftwareProjectSystem.Views;

namespace OneWare.SoftwareProjectSystem;

public class SoftwareProjectManager : IProjectManager
{
    private readonly IMainDockService _mainDockService;
    private readonly IProjectActionService _projectActionService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    public SoftwareProjectManager(IProjectExplorerService projectExplorerService, IMainDockService mainDockService,
        IWindowService windowService, IProjectActionService projectActionService)
    {
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _windowService = windowService;
        _projectActionService = projectActionService;

        _projectExplorerService.RegisterConstructContextMenu(ConstructContextMenu);
    }

    public string Extension => SoftwareProjectRoot.ProjectFileExtension;

    public async Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        var root = new SoftwareProjectRoot(path);
        if (!await root.LoadAsync()) return null;
        await root.InitializeAsync();
        return root;
    }

    public async Task ReloadProjectAsync(IProjectRoot project)
    {
        if (project is not SoftwareProjectRoot root) return;
        await root.LoadAsync();
        await root.InitializeAsync();
    }

    public async Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return root is SoftwareProjectRoot softwareProject && await softwareProject.SaveAsync();
    }

    public async Task NewProjectDialogAsync()
    {
        await _windowService.ShowDialogAsync(new SoftwareProjectCreatorView
        {
            DataContext = ContainerLocator.Container.Resolve<SoftwareProjectCreatorViewModel>()
        });
    }

    public async Task OpenSettingsDialogAsync(SoftwareProjectRoot root)
    {
        await _windowService.ShowDialogAsync(new SoftwareProjectSettingsView
        {
            DataContext = ContainerLocator.Container.Resolve<SoftwareProjectSettingsViewModel>(
                (typeof(SoftwareProjectRoot), root))
        });
    }

    private void ConstructContextMenu(IReadOnlyList<IProjectExplorerNode> selected, IList<MenuItemModel> menuItems)
    {
        if (selected is [IProjectFile { Root: SoftwareProjectRoot fileRoot } file] &&
            Build.SimpleBuildSystem.IsSource(file.RelativePath))
        {
            if (fileRoot.IsCompileExcluded(file.RelativePath))
                menuItems.Add(new MenuItemModel("IncludeCompilation")
                {
                    Header = "Include into build",
                    Command = new AsyncRelayCommand(() =>
                    {
                        fileRoot.RemoveCompileExcluded(file.RelativePath);
                        return SaveProjectAsync(fileRoot);
                    })
                });
            else
                menuItems.Add(new MenuItemModel("ExcludeCompilation")
                {
                    Header = "Exclude from build",
                    Command = new AsyncRelayCommand(() =>
                    {
                        fileRoot.AddCompileExcluded(file.RelativePath);
                        return SaveProjectAsync(fileRoot);
                    })
                });
            return;
        }

        if (selected is not [SoftwareProjectRoot root]) return;

        menuItems.Add(new MenuItemModel("Build")
        {
            Header = "Build",
            Command = new AsyncRelayCommand(() => _projectActionService.ExecuteAsync(ProjectActionKind.Build, root),
                () => _projectActionService.CanExecute(ProjectActionKind.Build, root)),
            Icon = new IconModel("CreateIcon")
        });
        menuItems.Add(new MenuItemModel("Clean")
        {
            Header = "Clean",
            Command = new AsyncRelayCommand(() => _projectActionService.ExecuteAsync(ProjectActionKind.Clean, root),
                () => _projectActionService.CanExecute(ProjectActionKind.Clean, root)),
            Icon = new IconModel("Material.DeleteSweep")
        });
        menuItems.Add(new MenuItemModel("Save")
        {
            Header = "Save",
            Command = new AsyncRelayCommand(() => SaveProjectAsync(root)),
            Icon = new IconModel("VsImageLib.Save16XMd")
        });
        menuItems.Add(new MenuItemModel("Reload")
        {
            Header = "Reload",
            Command = new AsyncRelayCommand(() => _projectExplorerService.ReloadProjectAsync(root)),
            Icon = new IconModel("VsImageLib.RefreshGrey16X")
        });
        menuItems.Add(new MenuItemModel("ProjectSettings")
        {
            Header = "Project Settings",
            Command = new AsyncRelayCommand(() => OpenSettingsDialogAsync(root)),
            Icon = new IconModel("Material.SettingsOutline")
        });
        menuItems.Add(new MenuItemModel("Edit")
        {
            Header = $"Edit {Path.GetFileName(root.ProjectFilePath)}",
            Command = new AsyncRelayCommand(() => _mainDockService.OpenFileAsync(root.ProjectFilePath))
        });
    }
}
