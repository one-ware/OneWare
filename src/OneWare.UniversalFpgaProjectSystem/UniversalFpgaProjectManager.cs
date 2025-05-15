using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectManager : IProjectManager
{
    private readonly IDockService _dockService;
    private readonly FpgaService _fpgaService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;
    private readonly Func<UniversalFpgaProjectCreatorViewModel> _creatorViewModelFactory;
    private readonly Func<(Type, object), UniversalFpgaProjectSettingsEditorViewModel> _settingsViewModelFactory;

    public UniversalFpgaProjectManager(
        IProjectExplorerService projectExplorerService,
        IDockService dockService,
        IWindowService windowService,
        FpgaService fpgaService,
        Func<UniversalFpgaProjectCreatorViewModel> creatorViewModelFactory,
        Func<(Type, object), UniversalFpgaProjectSettingsEditorViewModel> settingsViewModelFactory)
    {
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _windowService = windowService;
        _fpgaService = fpgaService;
        _creatorViewModelFactory = creatorViewModelFactory;
        _settingsViewModelFactory = settingsViewModelFactory;

        _projectExplorerService.RegisterConstructContextMenu(ConstructContextMenu);
    }

    public string Extension => UniversalFpgaProjectRoot.ProjectFileExtension;

    public async Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        var root = await UniversalFpgaProjectParser.DeserializeAsync(path);
        if (root == null) return null;

        ProjectHelper.ImportEntries(root.FullPath, root);

        if (root.GetProjectProperty(nameof(UniversalFpgaProjectRoot.TopEntity)) is { } top &&
            root.SearchRelativePath(top.ToPlatformPath()) is { } entity)
        {
            root.TopEntity = entity;
        }

        if (root.GetProjectProperty(nameof(UniversalFpgaProjectRoot.Toolchain)) is { } toolchain &&
            _fpgaService.Toolchains.FirstOrDefault(x => x.Name == toolchain) is { } tc)
        {
            root.Toolchain = tc;
        }

        if (root.GetProjectProperty(nameof(UniversalFpgaProjectRoot.Loader)) is { } loader &&
            _fpgaService.Loaders.FirstOrDefault(x => x.Name == loader) is { } l)
        {
            root.Loader = l;
        }

        foreach (var testBench in root.GetProjectPropertyArray(nameof(UniversalFpgaProjectRoot.TestBenches)) ?? Enumerable.Empty<string>())
        {
            if (root.SearchRelativePath(testBench.ToPlatformPath()) is IProjectFile file)
                root.RegisterTestBench(file);
        }

        foreach (var exclude in root.GetProjectPropertyArray(nameof(UniversalFpgaProjectRoot.CompileExcluded)) ?? Enumerable.Empty<string>())
        {
            if (root.SearchRelativePath(exclude.ToPlatformPath()) is IProjectFile file)
                root.RegisterCompileExcluded(file);
        }

        foreach (var step in root.GetProjectPropertyArray(nameof(UniversalFpgaProjectRoot.PreCompileSteps)) ?? Enumerable.Empty<string>())
        {
            if (_fpgaService.GetPreCompileStep(step) is { } pre)
                root.RegisterPreCompileStep(pre);
        }

        return root;
    }

    public async Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return root is UniversalFpgaProjectRoot uFpga && await UniversalFpgaProjectParser.SerializeAsync(uFpga);
    }

    public async Task NewProjectDialogAsync()
    {
        await _windowService.ShowDialogAsync(new UniversalFpgaProjectCreatorView
        {
            DataContext = _creatorViewModelFactory()
        });
    }

    private void ConstructContextMenu(IReadOnlyList<IProjectExplorerNode> selected, IList<MenuItemViewModel> menuItems)
    {
        if (selected.Count != 1) return;

        switch (selected.First())
        {
            case UniversalFpgaProjectRoot root:
                menuItems.Add(new MenuItemViewModel("Save")
                {
                    Header = "Save",
                    Command = new AsyncRelayCommand(() => SaveProjectAsync(root)),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.Save16XMd")
                });
                menuItems.Add(new MenuItemViewModel("Reload")
                {
                    Header = "Reload",
                    Command = new AsyncRelayCommand(() => _projectExplorerService.ReloadAsync(root)),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.RefreshGrey16X")
                });
                menuItems.Add(new MenuItemViewModel("ProjectSettings")
                {
                    Header = "Project Settings",
                    Command = new RelayCommand(() => _ = OpenProjectSettingsDialogAsync(root)),
                    IconObservable = Application.Current!.GetResourceObservable("Material.SettingsOutline")
                });
                menuItems.Add(new MenuItemViewModel("Edit")
                {
                    Header = $"Edit {Path.GetFileName(root.ProjectFilePath)}",
                    Command = new AsyncRelayCommand(() =>
                        _dockService.OpenFileAsync(root.SearchFullPath(root.ProjectFilePath) as IProjectFile ??
                                                   _projectExplorerService.GetTemporaryFile(root.ProjectFilePath)))
                });
                break;

            case FpgaProjectFile { Root: UniversalFpgaProjectRoot projectRoot } file:
                HandleFpgaFileContextMenu(file, projectRoot, menuItems);
                break;
        }
    }

    private void HandleFpgaFileContextMenu(FpgaProjectFile file, UniversalFpgaProjectRoot projectRoot, IList<MenuItemViewModel> menuItems)
    {
        if (file.Extension is not (".vhd" or ".vhdl" or ".v" or ".sv")) return;

        if (projectRoot.TopEntity == file)
        {
            menuItems.Add(new MenuItemViewModel("UnsetTopEntity")
            {
                Header = "Unset Top Entity",
                Command = new RelayCommand(() =>
                {
                    projectRoot.TopEntity = null;
                    _ = SaveProjectAsync(projectRoot);
                })
            });
        }
        else
        {
            menuItems.Add(new MenuItemViewModel("SetTopEntity")
            {
                Header = "Set Top Entity",
                Command = new RelayCommand(() =>
                {
                    projectRoot.TopEntity = file;
                    _ = SaveProjectAsync(projectRoot);
                })
            });
        }

        if (!projectRoot.CompileExcluded.Contains(file))
        {
            menuItems.Add(new MenuItemViewModel("ExcludeCompilation")
            {
                Header = "Exclude from compile",
                Command = new RelayCommand(() =>
                {
                    projectRoot.RegisterCompileExcluded(file);
                    _ = SaveProjectAsync(projectRoot);
                })
            });
        }
        else
        {
            menuItems.Add(new MenuItemViewModel("IncludeCompilation")
            {
                Header = "Include into compile",
                Command = new RelayCommand(() =>
                {
                    projectRoot.UnregisterCompileExcluded(file);
                    _ = SaveProjectAsync(projectRoot);
                })
            });
        }

        if (!projectRoot.TestBenches.Contains(file))
        {
            menuItems.Add(new MenuItemViewModel("MarkTestBench")
            {
                Header = "Mark as TestBench",
                Command = new RelayCommand(() =>
                {
                    projectRoot.RegisterTestBench(file);
                    _ = SaveProjectAsync(projectRoot);
                }),
                IconObservable = Application.Current!.GetResourceObservable("VSImageLib.AddTest_16x")
            });
        }
        else
        {
            menuItems.Add(new MenuItemViewModel("UnmarkTestBench")
            {
                Header = "Unmark as TestBench",
                Command = new RelayCommand(() =>
                {
                    projectRoot.UnregisterTestBench(file);
                    _ = SaveProjectAsync(projectRoot);
                }),
                IconObservable = Application.Current!.GetResourceObservable("VSImageLib.RemoveSingleDriverTest_16x")
            });
        }
    }

    private async Task OpenProjectSettingsDialogAsync(UniversalFpgaProjectRoot root)
    {
        await _windowService.ShowDialogAsync(new UniversalFpgaProjectSettingsEditorView
        {
            DataContext = _settingsViewModelFactory((typeof(UniversalFpgaProjectRoot), root))
        });
    }
}
