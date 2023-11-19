using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Shared.Helpers;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem;

public class UniversalFpgaProjectManager : IProjectManager
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    private readonly FpgaService _fpgaService;

    public UniversalFpgaProjectManager(IProjectExplorerService projectExplorerService, IDockService dockService,
        IWindowService windowService, FpgaService fpgaService)
    {
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _windowService = windowService;
        _fpgaService = fpgaService;

        _projectExplorerService.RegisterConstructContextMenu(ConstructContextMenu);
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

        if (root == null) return root;

        ProjectHelper.ImportEntries(root.FullPath, root);

        //Load Properties
        var top = root.Properties["TopEntity"];
        if (top != null && root.Search(top.ToString()) is { } entity)
        {
            root.TopEntity = entity;
        }
        
        var toolchain = root.Properties["Toolchain"];
        if (toolchain != null && _fpgaService.Toolchains.FirstOrDefault(x => x.Name == toolchain.ToString()) is {} tc)
        {
            root.Toolchain = tc;
        }
        
        var loader = root.Properties["Loader"];
        if (loader != null && _fpgaService.Loaders.FirstOrDefault(x => x.Name == loader.ToString()) is {} l)
        {
            root.Loader = l;
        }

        return root;
    }

    public async Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        return root is UniversalFpgaProjectRoot uFpga && await UniversalFpgaProjectParser.SerializeAsync(uFpga);
    }

    public IEnumerable<MenuItemModel> ConstructContextMenu(IList<IProjectEntry> selected)
    {
        if (selected.Count == 1)
        {
            switch (selected.First())
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
                        Command = new AsyncRelayCommand(() =>
                            _dockService.OpenFileAsync(root.Search(root.ProjectFilePath) as IProjectFile 
                                                       ?? _projectExplorerService.GetTemporaryFile(root.ProjectFilePath))),
                    };
                    break;
                case IProjectFile { Root: UniversalFpgaProjectRoot universalFpgaProjectRoot } file:
                    if (universalFpgaProjectRoot.TopEntity == file)
                    {
                        yield return new MenuItemModel("Unset Top Entity")
                        {
                            Header = $"Unset Top Entity",
                            Command = new RelayCommand(() =>
                            {
                                universalFpgaProjectRoot.TopEntity = null;
                                _ = SaveProjectAsync(universalFpgaProjectRoot);
                            }),
                        };
                    }
                    else if(file.Extension is ".vhd" or ".vhdl" or ".v")
                    {
                        yield return new MenuItemModel("Set Top Entity")
                        {
                            Header = $"Set Top Entity",
                            Command = new RelayCommand(() =>
                            {
                                universalFpgaProjectRoot.TopEntity = file;
                                _ = SaveProjectAsync(universalFpgaProjectRoot);
                            }),
                        };
                    }

                    break;
            }
        }
    }
}