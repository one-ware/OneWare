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
    private readonly FpgaService _fpgaService;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    public UniversalFpgaProjectManager(IProjectExplorerService projectExplorerService, IMainDockService mainDockService,
        IWindowService windowService, FpgaService fpgaService)
    {
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _windowService = windowService;
        _fpgaService = fpgaService;

        _projectExplorerService.RegisterConstructContextMenu(ConstructContextMenu);
    }

    public string Extension => UniversalFpgaProjectRoot.ProjectFileExtension;

    public async Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        var root = await UniversalFpgaProjectParser.DeserializeAsync(path);

        if (root == null) return root;

        var toolchain = root.GetProjectProperty(nameof(UniversalFpgaProjectRoot.Toolchain));
        if (toolchain != null && _fpgaService.Toolchains.FirstOrDefault(x => x.Name == toolchain) is { } tc)
            root.Toolchain = tc;

        var loader = root.GetProjectProperty(nameof(UniversalFpgaProjectRoot.Loader));
        if (loader != null && _fpgaService.Loaders.FirstOrDefault(x => x.Name == loader) is { } l) root.Loader = l;

        var preCompileSteps = root.GetProjectPropertyArray(nameof(UniversalFpgaProjectRoot.PreCompileSteps));
        if (preCompileSteps != null)
            foreach (var preCompileStep in preCompileSteps)
                if (_fpgaService.GetPreCompileStep(preCompileStep) is { } pre)
                    root.RegisterPreCompileStep(pre);

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
            DataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCreatorViewModel>()
        });
    }

    private void ConstructContextMenu(IReadOnlyList<IProjectExplorerNode> selected, IList<MenuItemViewModel> menuItems)
    {
        if (selected.Count == 1)
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
                            _mainDockService.OpenFileAsync(_projectExplorerService.GetTemporaryFile(root.ProjectFilePath)))
                    });
                    break;
                case FpgaProjectFile { Root: UniversalFpgaProjectRoot universalFpgaProjectRoot } file:
                    if (file.Extension is ".vhd" or ".vhdl" or ".v" or ".sv")
                    {
                        //Set Top
                        if (universalFpgaProjectRoot.TopEntity == file.RelativePath)
                            menuItems.Add(new MenuItemViewModel("Unset Top Entity")
                            {
                                Header = "Unset Top Entity",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.TopEntity = null;
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                })
                            });
                        else
                            menuItems.Add(new MenuItemViewModel("Set Top Entity")
                            {
                                Header = "Set Top Entity",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.TopEntity = file.RelativePath;
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                })
                            });

                        //Exclude from compile
                        if (!universalFpgaProjectRoot.IsCompileExcluded(file.RelativePath))
                            menuItems.Add(new MenuItemViewModel("ExcludeCompilation")
                            {
                                Header = "Exclude from compile",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.AddCompileExcluded(file.RelativePath);
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                })
                            });
                        else
                            menuItems.Add(new MenuItemViewModel("IncludeCompilation")
                            {
                                Header = "Include into compile",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.RemoveCompileExcluded(file.RelativePath);
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                })
                            });

                        //Testbenches
                        if (!universalFpgaProjectRoot.IsTestBench(file.RelativePath))
                            menuItems.Add(new MenuItemViewModel("MarkTestBench")
                            {
                                Header = "Mark as TestBench",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.AddTestBench(file.RelativePath);
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                }),
                                IconObservable = Application.Current!.GetResourceObservable("VSImageLib.AddTest_16x")
                            });
                        else
                            menuItems.Add(new MenuItemViewModel("UnmarkTestBench")
                            {
                                Header = "Unmark as TestBench",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.RemoveTestBench(file.RelativePath);
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                }),
                                IconObservable =
                                    Application.Current!.GetResourceObservable("VSImageLib.RemoveSingleDriverTest_16x")
                            });
                    }

                    break;
            }
    }

    private async Task OpenProjectSettingsDialogAsync(UniversalFpgaProjectRoot root)
    {
        // UniversalFpgaProjectRoot root
        await _windowService.ShowDialogAsync(new UniversalFpgaProjectSettingsEditorView
        {
            DataContext =
                ContainerLocator.Container.Resolve<UniversalFpgaProjectSettingsEditorViewModel>((
                    typeof(UniversalFpgaProjectRoot), root))
        });
    }
}