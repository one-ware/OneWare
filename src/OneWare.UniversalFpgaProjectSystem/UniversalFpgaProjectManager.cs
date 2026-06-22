using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
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
    private readonly IOutputService _outputService;

    public UniversalFpgaProjectManager(IProjectExplorerService projectExplorerService, IMainDockService mainDockService,
        IWindowService windowService, FpgaService fpgaService, IOutputService outputService)
    {
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _windowService = windowService;
        _fpgaService = fpgaService;
        _outputService = outputService;

        _projectExplorerService.RegisterConstructContextMenu(ConstructContextMenu);
    }

    public string Extension => UniversalFpgaProjectRoot.ProjectFileExtension;

    public async Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        var root = new UniversalFpgaProjectRoot(path);
        
        await root.LoadAsync(_fpgaService.ProjectPropertyMigrations);
        await MigrateLegacyTopEntityAsync(root);

        foreach (var entryModification in _fpgaService.ProjectEntryModificationHandlers)
        {
            root.RegisterProjectEntryModification(entryModification);
        }

        await root.InitializeAsync();

        await UpdateTopEntityFilePathAsync(root);

        root.Properties.ProjectPropertyChanged += (_, args) =>
        {
            if (args.PropertyName.Equals("topEntity", StringComparison.OrdinalIgnoreCase))
                _ = UpdateTopEntityFilePathAsync(root);
        };

        return root;
    }

    public async Task ReloadProjectAsync(IProjectRoot project)
    {
        if (project is not UniversalFpgaProjectRoot root) return;
        
        await root.LoadAsync(_fpgaService.ProjectPropertyMigrations);
        await MigrateLegacyTopEntityAsync(root);
        await root.InitializeAsync();

        await UpdateTopEntityFilePathAsync(root);
        
        //TODO reload open files
        var filesOpenInProject = _mainDockService.OpenFiles
            .Where(x => IsUnderRoot(project.RootFolderPath, x.Value.FullPath))
            .Select(x => new { Key = x.Key, ViewModel = x.Value })
            .ToList();
        
        // foreach (var refreshed in filesOpenInProject)
        //     if (_mainDockService.OpenFiles.TryGetValue(refreshed.Key, out var vm))
        //         vm.InitializeContent();
    }

    /// <summary>
    /// Detects a legacy <c>topEntity</c> value that is a file path rather than an entity name,
    /// opens the referenced file, and replaces the value with the real entity/module name found inside.
    /// Falls back to the file name without extension when the file cannot be parsed.
    /// </summary>
    private async Task MigrateLegacyTopEntityAsync(UniversalFpgaProjectRoot root)
    {
        var legacyValue = root.Properties.GetString("topEntity");
        if (legacyValue == null) return;

        // Detect path-like values: contains a directory separator, OR has a known HDL extension
        var looksLikePath = legacyValue.Contains('/') || legacyValue.Contains('\\') ||
                            Path.GetExtension(legacyValue) is ".vhd" or ".vhdl" or ".v" or ".sv";
        if (!looksLikePath) return;

        // Best-effort entity name derived from the file name
        var candidateName = Path.GetFileNameWithoutExtension(legacyValue);

        // Try to read the actual entity/module name from the file
        var file = root.GetFile(legacyValue);
        if (file != null)
        {
            var provider = _fpgaService.GetNodeProviderByExtension(file.Extension);
            if (provider != null)
            {
                try
                {
                    var entities = (await provider.ExtractTopEntitiesAsync(file)).ToList();
                    if (entities.Count == 1)
                    {
                        candidateName = entities[0];
                    }
                    else if (entities.Count > 1)
                    {
                        // Prefer one whose name matches the file name; otherwise take the first
                        candidateName = entities.FirstOrDefault(e =>
                                            string.Equals(e, candidateName, StringComparison.OrdinalIgnoreCase))
                                        ?? entities[0];
                    }
                }
                catch
                {
                    // Ignore parse errors — candidateName stays as the file name without extension
                }
            }
        }

        root.Properties.SetString("topEntity", candidateName);
    }
    
    private static bool IsUnderRoot(string rootPath, string filePath)
    {
        var relative = Path.GetRelativePath(rootPath, filePath);
        return !relative.StartsWith("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(relative);
    }

    public async Task<bool> SaveProjectAsync(IProjectRoot root)
    {
        if (root is not UniversalFpgaProjectRoot fpgaProject) return false;
        
        var result = await fpgaProject.SaveAsync();

        if(result)
            fpgaProject.LastSaveTime = DateTime.Now;
        
        return result;
    }

    public async Task NewProjectDialogAsync()
    {
        await _windowService.ShowDialogAsync(new UniversalFpgaProjectCreatorView
        {
            DataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCreatorViewModel>()
        });
    }

    private void ConstructContextMenu(IReadOnlyList<IProjectExplorerNode> selected, IList<MenuItemModel> menuItems)
    {
        if (selected.Count == 1)
            switch (selected.First())
            {
                case UniversalFpgaProjectRoot root:
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
                    menuItems.Add(new MenuItemModel("Clean")
                    {
                        Header = "Clean",
                        Command = new AsyncRelayCommand(() =>
                            CleanBuildFoldersAsync(root))
                    });
                    menuItems.Add(new MenuItemModel("ProjectSettings")
                    {
                        Header = "Project Settings",
                        Command = new RelayCommand(() => _ = OpenProjectSettingsDialogAsync(root)),
                        Icon = new IconModel("Material.SettingsOutline")
                    });
                    menuItems.Add(new MenuItemModel("Edit")
                    {
                        Header = $"Edit {Path.GetFileName(root.ProjectFilePath)}",
                        Command = new AsyncRelayCommand(() =>
                            _mainDockService.OpenFileAsync(root.ProjectFilePath))
                    });
                    break;
                case FpgaProjectFile { Root: UniversalFpgaProjectRoot universalFpgaProjectRoot } file:
                    if (file.Extension is ".vhd" or ".vhdl" or ".v" or ".sv")
                    {
                        //Set Top Entity
                        var setTopEntityMenu = new MenuItemModel("SetTopEntity")
                        {
                            Header = "Set Top Entity",
                            Icon = new IconModel("VsImageLib2019.DownloadOverlay16X"),
                            Items = new ObservableCollection<MenuItemModel>()
                        };
                        menuItems.Add(setTopEntityMenu);
                        _ = PopulateTopEntityMenuItemsAsync(setTopEntityMenu, file, universalFpgaProjectRoot);

                        //Exclude from compile
                        if (!universalFpgaProjectRoot.IsCompileExcluded(file.RelativePath))
                            menuItems.Add(new MenuItemModel("ExcludeCompilation")
                            {
                                Header = "Exclude from compile",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.AddCompileExcluded(file.RelativePath);
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                })
                            });
                        else
                            menuItems.Add(new MenuItemModel("IncludeCompilation")
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
                            menuItems.Add(new MenuItemModel("MarkTestBench")
                            {
                                Header = "Mark as TestBench",
                                Command = new RelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.AddTestBench(file.RelativePath);
                                    _ = SaveProjectAsync(universalFpgaProjectRoot);
                                }),
                                Icon = new IconModel("VSImageLib.AddTest_16x")
                            });
                        else
                            menuItems.Add(new MenuItemModel("UnmarkTestBench")
                            {
                                Header = "Unmark as TestBench",
                                Command = new AsyncRelayCommand(() =>
                                {
                                    universalFpgaProjectRoot.RemoveTestBench(file.RelativePath);
                                    return SaveProjectAsync(universalFpgaProjectRoot);
                                }),
                                Icon = new IconModel("VSImageLib.RemoveSingleDriverTest_16x")
                            });
                    }

                    break;
            }
    }

    private async Task PopulateTopEntityMenuItemsAsync(MenuItemModel menu, IProjectFile file,
        UniversalFpgaProjectRoot root)
    {
        var nodeProvider = _fpgaService.GetNodeProviderByExtension(file.Extension);
        if (nodeProvider == null) return;

        List<string> entities;
        try
        {
            entities = (await nodeProvider.ExtractTopEntitiesAsync(file)).ToList();
        }
        catch
        {
            return;
        }

        if (menu.Items == null) return;

        foreach (var entity in entities)
        {
            var entityName = entity;
            var isCurrent = root.TopEntity == entityName &&
                            root.TopEntityFilePath != null &&
                            file.RelativePath.EqualPaths(root.TopEntityFilePath);

            menu.Items.Add(new MenuItemModel(entityName)
            {
                Header = entityName,
                Icon = isCurrent ? new IconModel("BoxIcons.RegularCheck") : null,
                Command = new RelayCommand(() =>
                {
                    root.TopEntity = entityName;
                    root.TopEntityFilePath = file.RelativePath;
                    _ = SaveProjectAsync(root);
                })
            });
        }
    }

    /// <summary>
    /// Recomputes <see cref="UniversalFpgaProjectRoot.TopEntityFilePath"/> by locating the file
    /// that contains the current <see cref="UniversalFpgaProjectRoot.TopEntity"/>.
    /// </summary>
    private async Task UpdateTopEntityFilePathAsync(UniversalFpgaProjectRoot root)
    {
        if (string.IsNullOrEmpty(root.TopEntity))
        {
            root.TopEntityFilePath = null;
            return;
        }

        var allEntities = await _fpgaService.GetAllTopEntitiesAsync(root);
        var match = allEntities.FirstOrDefault(x => x.TopEntity == root.TopEntity);
        root.TopEntityFilePath = match?.File.RelativePath;
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

    private async Task CleanBuildFoldersAsync(UniversalFpgaProjectRoot root)
    {
        var buildFolderPath = Path.Combine(root.RootFolderPath, "build");
        _outputService.WriteLine($"Cleaning build folders: {buildFolderPath}");
        if (Directory.Exists(buildFolderPath))
        {
            await Task.Run(() =>
            {
                Directory.Delete(buildFolderPath, true);
                Directory.CreateDirectory(buildFolderPath);
            });
        }
    }
}
