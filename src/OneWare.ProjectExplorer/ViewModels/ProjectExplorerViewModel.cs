using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.Services;

namespace OneWare.ProjectExplorer.ViewModels;

public class ProjectExplorerViewModel : ProjectViewModelBase, IProjectExplorerService
{
    public const string IconKey = "EvaIcons.FolderOutline";
    
    private readonly IFileWatchService _fileWatchService;
    private readonly ILanguageManager _languageManager;

    private readonly string _lastProjectsFile;
    private readonly IMainDockService _mainDockService;

    private readonly IPaths _paths;
    private readonly IProjectManagerService _projectManagerService;

    private readonly LinkedList<string> _recentProjects = new();
    private readonly int _recentProjectsCapacity = 4;
    private readonly string _recentProjectsFile;

    private readonly List<Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemModel>>> _registerContextMenu =
        new();

    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    private IProjectRoot? _activeProject;

    public ProjectExplorerViewModel(IApplicationStateService applicationStateService, IPaths paths,
        IMainDockService mainDockService,
        IWindowService windowService, ISettingsService settingsService,
        IProjectManagerService projectManagerService, IFileWatchService fileWatchService,
        ILanguageManager languageManager)
        : base(IconKey)
    {
        ApplicationStateService = applicationStateService;
        _paths = paths;
        _mainDockService = mainDockService;
        _windowService = windowService;
        _settingsService = settingsService;
        _projectManagerService = projectManagerService;
        _fileWatchService = fileWatchService;
        _languageManager = languageManager;

        _lastProjectsFile = Path.Combine(_paths.AppDataDirectory, "LastProjects.json");
        _recentProjectsFile = Path.Combine(_paths.AppDataDirectory, "RecentProjects.json");

        Id = "ProjectExplorer";
        
        ApplicationStateService.RegisterShutdownTask(ShutdownAsync);
    }

    public IApplicationStateService ApplicationStateService { get; }

    public IProjectRoot? ActiveProject
    {
        get => _activeProject;
        set
        {
            if (_activeProject is not null) _activeProject.IsActive = false;
            SetProperty(ref _activeProject, value);
            if (_activeProject is not null) _activeProject.IsActive = true;
        }
    }
    
    public event EventHandler<IProjectRoot>? ProjectRemoved;

    public override void InitializeContent()
    {
        base.InitializeContent();
        Title = "Explorer";
    }

    public override void AddProject(IProjectRoot project)
    {
        base.AddProject(project);
        _fileWatchService.RegisterProject(project);
    }

    public async Task TryCloseProjectAsync(IProjectRoot project)
    {
        var openFiles = _mainDockService.OpenFiles
            .Where(x => IsUnderRoot(project.RootFolderPath, x.Value.FullPath))
            .ToList();

        foreach (var tab in openFiles)
            if (!await _mainDockService.CloseFileAsync(tab.Value.FullPath))
                return;

        ProjectRemoved?.Invoke(this, project);
        _fileWatchService.UnregisterProject(project);

        var activeProj = project == ActiveProject;

        Projects.Remove(project);

        if (Projects.Count == 0) //Avalonia bugfix
            ClearSelection();

        if (_recentProjects.Count == _recentProjectsCapacity)
            _recentProjects.RemoveLast();
        if (!_recentProjects.Contains(project.ProjectPath))
            _recentProjects.AddFirst(project.ProjectPath);

        if (activeProj)
            ActiveProject = Projects.Count > 0 ? Projects[0] : null;
    }

    public async Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager)
    {
        var folderPath = await StorageProviderHelper.SelectFolderAsync(
            _mainDockService.GetWindowOwner(this) ?? throw new NullReferenceException("Window"), "Select Folder Path",
            _paths.ProjectsDirectory);

        if (folderPath == null) return null;

        var result = await LoadProjectAsync(folderPath, manager);

        _ = SaveLastProjectsFileAsync();

        return result;
    }

    public IEnumerable<string> LoadRecentProjects()
    {
        if (PlatformHelper.Platform is PlatformId.Wasm || !File.Exists(_recentProjectsFile))
            return [];

        try
        {
            using var stream = File.OpenRead(_recentProjectsFile);
            var recentFiles = JsonSerializer.Deserialize<string[]>(stream) ?? [];
            for (var i = 0; i < Math.Min(_recentProjectsCapacity, recentFiles.Length); i++)
            {
                var path = recentFiles[i];
                if (!File.Exists(path) && !Directory.Exists(path))
                    continue;

                _recentProjects.AddLast(path);
            }

            return _recentProjects;
        }
        catch
        {
            return [];
        }
    }

    public async Task<IProjectRoot?> LoadProjectFileDialogAsync(IProjectManager manager,
        params FilePickerFileType[]? filters)
    {
        var filePath = await StorageProviderHelper.SelectFileAsync(
            _mainDockService.GetWindowOwner(this) ?? throw new NullReferenceException("Window"), "Select Project File",
            _paths.ProjectsDirectory, filters);

        if (filePath == null) return null;

        var result = await LoadProjectAsync(filePath, manager);

        _ = SaveLastProjectsFileAsync();

        return result;
    }

    public async Task<IProjectRoot?> LoadProjectAsync(string path, IProjectManager manager, bool expand = true,
        bool setActive = true)
    {
        var project = await manager.LoadProjectAsync(path);
        
        if (project == null)
            return null;
        
        AddProject(project);

        if (expand)
        {
            await Task.Delay(10);
            project.IsExpanded = true;
        }
        
        if (setActive) ActiveProject = project;

        return project;
    }

    public void DoubleTab()
    {
        if (SelectedItems is [IProjectFile file])
            _ = _mainDockService.OpenFileAsync(file.FullPath);
        else if (SelectedItems is [IProjectFolder folder])
            folder.IsExpanded = !folder.IsExpanded;
    }

    public void ConstructContextMenu(TopLevel topLevel)
    {
        var menuItems = new List<MenuItemModel>();

        if (SelectedItems is [{ } item])
        {
            switch (item)
            {
                case IProjectFile file:
                    menuItems.Add(new MenuItemModel("Open")
                    {
                        Header = "Open",
                        Command = new AsyncRelayCommand(() => _mainDockService.OpenFileAsync(file.FullPath))
                    });
                    break;
                case IProjectFolder folder:

                    if (folder is IProjectRoot { IsActive: false } inactiveRoot)
                        menuItems.Add(new MenuItemModel("SetActive")
                        {
                            Header = "Set as Active Project",
                            Command = new RelayCommand(() => ActiveProject = inactiveRoot),
                            Icon = new IconModel("VsCodeLight.Debug-Start")
                        });

                    menuItems.Add(new MenuItemModel("Add")
                    {
                        Header = "Add",
                        Items = new ObservableCollection<MenuItemModel>
                        {
                            new("NewFolder")
                            {
                                Header = "New Folder",
                                Command = new RelayCommand(() => _ = CreateFolderDialogAsync(folder)),
                                Icon = new IconModel("VsImageLib.OpenFolder16X")
                            },
                            new("NewFile")
                            {
                                Header = "New File",
                                Command = new RelayCommand(() => _ = CreateFileDialogAsync(folder)),
                                Icon = new IconModel("VsImageLib.NewFile16X")
                            },
                            new("ExistingFolder")
                            {
                                Header = "Import Folder",
                                Command = new RelayCommand(() => _ = ImportFolderDialogAsync(folder)),
                                Icon = new IconModel("VsImageLib.Folder16X")
                            },
                            new("ExistingFile")
                            {
                                Header = "Import File",
                                Command = new RelayCommand(() => _ = ImportFileDialogAsync(folder)),
                                Icon = new IconModel("VsImageLib.File16X")
                            }
                        }
                    });
                    break;
            }

            foreach (var reg in _registerContextMenu) reg.Invoke(SelectedItems, menuItems);

            if (item is IProjectRoot root)
                menuItems.Add(new MenuItemModel("Close")
                {
                    Header = "Close",
                    Command = new AsyncRelayCommand(() => TryCloseProjectAsync(root))
                });

            if (item is IProjectEntry entry)
            {
                if (item is not IProjectRoot)
                    menuItems.Add(new MenuItemModel("Edit")
                    {
                        Header = "Edit",
                        Items = new ObservableCollection<MenuItemModel>
                        {
                            // new MenuItemModel("Cut")
                            // {
                            //     Header = "Cut",
                            //     Command = new RelayCommand(() => _ = CutAsync(entry)),
                            //     ImageIconModel = new IconModel("MaterialDesign.DeleteForever")
                            // },
                            new("Copy")
                            {
                                Header = "Copy",
                                Command = new RelayCommand(() => _ = CopyAsync(topLevel)),
                                Icon = new IconModel("BoxIcons.RegularCopy"),
                                InputGesture = new KeyGesture(Key.C, KeyModifiers.Control)
                            },
                            new("Paste")
                            {
                                Header = "Paste",
                                Command = new RelayCommand(() => _ = PasteAsync(topLevel)),
                                Icon = new IconModel("BoxIcons.RegularPaste"),
                                InputGesture = new KeyGesture(Key.V, KeyModifiers.Control)
                            },
                            new("Delete")
                            {
                                Header = "Delete",
                                Command = new RelayCommand(() => _ = DeleteDialogAsync(entry)),
                                Icon = new IconModel("MaterialDesign.DeleteForever")
                            },
                            new("Rename")
                            {
                                Header = "Rename",
                                Command = new RelayCommand(() => _ = RenameDialogAsync(entry)),
                                Icon = new IconModel("VsImageLib.Rename16X")
                            }
                        }
                    });

                menuItems.Add(new MenuItemModel("OpenFileViewer")
                {
                    Header = "Open in File Manager",
                    Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(entry.FullPath)),
                    Icon = new IconModel("VsImageLib.OpenFolder16Xc")
                });
            }
        }
        else if (SelectedItems.Count > 1)
        {
            if (SelectedItems.All(x => x is IProjectEntry and not IProjectRoot))
                menuItems.Add(new MenuItemModel("Edit")
                {
                    Header = "Edit",
                    Items = new ObservableCollection<MenuItemModel>
                    {
                        // new MenuItemModel("Cut")
                        // {
                        //     Header = "Cut",
                        //     Command = new RelayCommand(() => _ = CutAsync(entry)),
                        //     ImageIconModel = new IconModel("MaterialDesign.DeleteForever")
                        // },
                        new("Delete")
                        {
                            Header = "Delete",
                            Command = new RelayCommand(() =>
                                _ = DeleteDialogAsync(SelectedItems.Cast<IProjectEntry>().ToArray())),
                            Icon = new IconModel("MaterialDesign.DeleteForever")
                        }
                    }
                });


            foreach (var reg in _registerContextMenu) reg.Invoke(SelectedItems, menuItems);
        }

        TreeViewContextMenu = menuItems;
    }

    public async Task OpenFileDialogAsync()
    {
        var file = await StorageProviderHelper.SelectFileAsync(_mainDockService.GetWindowOwner(this)!, "Select File",
            null);

        if (file != null) await _mainDockService.OpenFileAsync(file);
    }

    private async Task<bool> AskForIncludeDialogAsync(IProjectRoot root, string relativePath)
    {
        if (!root.IsPathIncluded(relativePath))
        {
            var dialogResult = await _windowService.ShowYesNoCancelAsync("Warning",
                $"{Path.GetFileName(relativePath)} is not included in {root.Header}! Do you want to include it?",
                MessageBoxIcon.Warning, _mainDockService.GetWindowOwner(this));

            switch (dialogResult)
            {
                case MessageBoxStatus.Canceled:
                    return false;
                case MessageBoxStatus.Yes:
                    root.IncludePath(relativePath);
                    _ = SaveProjectAsync(root);
                    break;
            }
        }

        return true;
    }

    #region Create

    public async Task CreateFileDialogAsync(IProjectFolder parent)
    {
        var newFile = await _windowService.ShowInputAsync("Create File", "Enter a name for the new file!",
            MessageBoxIcon.Info,
            "NewFile.txt", _mainDockService.GetWindowOwner(this));

        if (!string.IsNullOrWhiteSpace(newFile))
        {
            if (!parent.Root.IsPathIncluded(newFile))
            {
                parent.Root.IncludePath(newFile);
                _ = SaveProjectAsync(parent.Root);
            }

            var f = parent.AddFile(newFile, true);
            await Task.Delay(10);
            parent.IsExpanded = true;
            await _mainDockService.OpenFileAsync(f.FullPath);
        }
    }

    public async Task CreateFolderDialogAsync(IProjectFolder parent)
    {
        var newFolder = await _windowService.ShowInputAsync("Create Folder", "Enter a name for the new folder!",
            MessageBoxIcon.Info,
            "NewFolder", _mainDockService.GetWindowOwner(this));

        if (!string.IsNullOrWhiteSpace(newFolder))
        {
            parent.AddFolder(newFolder, true);
            await Task.Delay(10);
            parent.IsExpanded = true;
        }
    }

    #endregion

    #region Remove and Delete

    public async Task DeleteDialogAsync(params IProjectEntry[] entries)
    {
        if (entries.Length < 1) return;

        var message = string.Empty;

        if (entries.Length == 1)
            message = entries[0] switch
            {
                IProjectRoot =>
                    $"Are you sure you want to delete the project {entries[0].Header} permanently? This will also delete all included files and folders",
                IProjectFolder => $"Are you sure you want to delete the folder {entries[0].Header} permanently?",
                IProjectFile => $"Are you sure you want to delete {entries[0].Header} permanently?",
                _ => message
            };
        else
            message = $"Are you sure you want to delete {entries.Length} objects permanently?";

        var result = await _windowService.ShowYesNoAsync("Warning", message, MessageBoxIcon.Warning,
            _mainDockService.GetWindowOwner(this));

        if (result == MessageBoxStatus.Yes)
            await DeleteAsync(entries);
    }

    public async Task DeleteAsync(params IProjectEntry[] entries)
    {
        foreach (var entry in entries) await DeleteAsync(entry);
    }

    private Task DeleteAsync(IProjectEntry entry)
    {
        try
        {
            if (entry is IProjectFolder folder)
                Directory.Delete(folder.FullPath, true);
            else
                File.Delete(entry.FullPath);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Error("File / Directory could not be deleted from storage!" + e);
        }
        
        return Task.CompletedTask;
    }

    public async Task RenameDialogAsync(IProjectEntry entry)
    {
        var newName = await _windowService.ShowInputAsync("Rename File", $"Enter a new name for {entry.Header}", MessageBoxIcon.Info,
            entry.Header);

        if (newName == null) return;
        
        var oldName = entry.Header;
        newName = newName.Trim();
        if (newName == oldName) return;
        var pathBase = Path.GetDirectoryName(entry.FullPath);

        if (!newName.IsValidFileName() || (entry is IProjectFolder && Path.HasExtension(newName)) ||
            pathBase == null || entry.TopFolder == null)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error($"Can't rename {entry.Header} to {newName}!");
            return;
        }

        var oldPath = entry.FullPath;
        var newPath = Path.Combine(pathBase, newName);

        try
        {
            if (entry is IProjectFile)
            {
                if (File.Exists(newPath)) throw new Exception($"File {newPath} does already exist!");
                File.Move(oldPath, newPath);
            }
            else if (entry is IProjectFolder)
            {
                if (Directory.Exists(newPath)) throw new Exception($"Folder {newPath} does already exist!");
                Directory.Move(oldPath, newPath);
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    #endregion

    #region Import

    public async Task ImportFolderDialogAsync(IProjectFolder? destination = null)
    {
        destination ??= ActiveProject;

        if (destination == null)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(
                "Can't import folder if there is no active project selected. Please select an active project first",
                null, true, true);
            return;
        }

        var folders = await StorageProviderHelper.SelectFoldersAsync(_mainDockService.GetWindowOwner(this)!,
            "Import Folders to " + destination.Header,
            destination.FullPath);

        await ImportAsync(destination, true, true, folders.ToArray());
    }

    public async Task ImportFileDialogAsync(IProjectFolder? destination = null)
    {
        destination ??= ActiveProject;

        if (destination == null)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(
                "Can't import files if there is no active project selected. Please select an active project first",
                null, true, true);
            return;
        }

        var files = await StorageProviderHelper.SelectFilesAsync(_mainDockService.GetWindowOwner(this)!,
            "Import Files to " + destination.Header,
            destination.FullPath);

        await ImportAsync(destination, true, true, files.ToArray());
    }

    public async Task ImportAsync(IProjectFolder destination, bool copy, bool askForInclude, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!copy && path == destination.FullPath) continue;

            try
            {
                var attr = File.GetAttributes(path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    var destPath = Path.Combine(destination.FullPath, Path.GetFileName(path.TrimEnd('/', '\\')));
                    if (copy) PlatformHelper.CopyDirectory(path, destPath);
                    else Directory.Move(path, destPath);
                }
                else
                {
                    var destPath = Path.Combine(destination.FullPath, Path.GetFileName(path));
                    if (askForInclude)
                        await AskForIncludeDialogAsync(destination.Root,
                            Path.GetRelativePath(destination.Root.FullPath, destPath));
                    if (copy) PlatformHelper.CopyFile(path, destPath);
                    else File.Move(path, destPath);
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            }
        }
    }

    #endregion

    #region File Change

    public async Task ReloadProjectAsync(IProjectRoot project)
    {
        var manager = _projectManagerService.GetManager(project.ProjectTypeId);

        if (manager == null)
        {
            project.LoadingFailed = true;
            ContainerLocator.Container.Resolve<ILogger>()
                .Error($"Cannot reload {project.Header}. Manager not found!");
            
            project.LoadingFailed = true;
            return;
        }
        
        await manager.ReloadProjectAsync(project);
    }

    public Task SaveProjectAsync(IProjectRoot project)
    {
        var manager = _projectManagerService.GetManager(project.ProjectTypeId);
        if (manager == null) throw new NullReferenceException(nameof(manager));
        return manager.SaveProjectAsync(project);
    }

    public async Task<bool> SaveOpenFilesForProjectAsync(IProjectRoot project)
    {
        var saveTasks = _mainDockService.OpenFiles
            .Where(x => IsUnderRoot(project.RootFolderPath, x.Value.FullPath))
            .Select(x => x.Value.SaveAsync());

        var results = await Task.WhenAll(saveTasks);

        return results.All(x => x);
    }

    private static bool IsUnderRoot(string rootPath, string filePath)
    {
        var relative = Path.GetRelativePath(rootPath, filePath);
        return !relative.StartsWith("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(relative);
    }

    #endregion

    #region Copy and Paste, Drop Files

    public async Task DropAsync(IProjectFolder destination, bool warning, bool copy, params string[] paths)
    {
        if (warning)
        {
            var action = copy ? "copy" : "move";
            var result = await _windowService.ShowYesNoAsync("Warning",
                $"Are you sure you want to {action} {paths.Length} objects?", MessageBoxIcon.Warning);
            if (result is not MessageBoxStatus.Yes) return;
        }

        await ImportAsync(destination, copy, true, paths);
        destination.IsExpanded = true;
    }

    public async Task CopyAsync(TopLevel topLevel)
    {
        if (topLevel.Clipboard is not { } clipboard) return;

        var dataTransfer = await GetDataTransferFromItemsAsync(topLevel, SelectedItems);

        if (dataTransfer == null) return;
        await clipboard.SetDataAsync(dataTransfer);
    }

    private static async Task<DataTransfer?> GetDataTransferFromItemsAsync(TopLevel topLevel,
        IEnumerable<IProjectExplorerNode> items)
    {
        var dataTransfer = new DataTransfer();

        var storageItems = (await items
                .Where(x => x is IProjectEntry)
                .Cast<IProjectEntry>()
                .SelectAsync<IProjectEntry, IStorageItem?>(async x =>
                {
                    return x switch
                    {
                        IProjectFile => await topLevel.StorageProvider.TryGetFileFromPathAsync(x.FullPath),
                        IProjectFolder => await topLevel.StorageProvider.TryGetFolderFromPathAsync(x.FullPath),
                        _ => null
                    };
                }))
            .Where(x => x != null)
            .Cast<IStorageItem>()
            .ToArray();

        if (!storageItems.Any()) return null;

        foreach (var storageItem in storageItems)
            dataTransfer.Add(DataTransferItem.CreateFile(storageItem));

        return dataTransfer;
    }

    public async Task PasteAsync(TopLevel topLevel)
    {
        if (topLevel.Clipboard is not { } clipboard || SelectedItems is not [{ } selectedItem]) return;

        var target = selectedItem as IProjectFolder ?? selectedItem?.Parent as IProjectFolder;
        if (target == null) return;

        var data = await clipboard.TryGetDataAsync();
        if (data == null) return;
        try
        {
            var storageItems = await data.TryGetFilesAsync();
            if (storageItems != null)
                await ImportAsync(target, true, true, storageItems
                    .Select(x => x.TryGetLocalPath())
                    .Where(x => x != null)
                    .Cast<string>()
                    .ToArray());
        }
        finally
        {
            if (data is IDisposable disposable)
                disposable.Dispose();
        }

        target.IsExpanded = true;
    }

    public Task DeleteSelectedDialogAsync()
    {
        if (SelectedItems.Count == 0 || SelectedItems.Any(x => x is not IProjectEntry)) return Task.CompletedTask;

        return DeleteDialogAsync(SelectedItems.Cast<IProjectEntry>().ToArray());
    }

    #endregion

    #region LastProjectsFile

    private async Task<bool> ShutdownAsync()
    {
        await SaveRecentProjectsFileAsync();
        await SaveLastProjectsFileAsync();
        return true;
    }

    public async Task SaveRecentProjectsFileAsync()
    {
        if (PlatformHelper.Platform is PlatformId.Wasm)
            return;

        try
        {
            var serialization = _recentProjects.ToArray();
            await using var stream = File.OpenWrite(_recentProjectsFile);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, serialization,
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public async Task SaveLastProjectsFileAsync()
    {
        if (PlatformHelper.Platform is PlatformId.Wasm) return;

        try
        {
            var serialization = Projects
                .Select(x => new ProjectSerialization(x.ProjectTypeId, x.ProjectPath, x.IsExpanded, x.IsActive))
                .ToArray();
            await using var stream = File.OpenWrite(_lastProjectsFile);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, serialization, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public async Task OpenLastProjectsFileAsync()
    {
        if (PlatformHelper.Platform is PlatformId.Wasm) return;

        if (!File.Exists(_lastProjectsFile)) return;
        try
        {
            await using var stream = File.OpenRead(_lastProjectsFile);
            var lastProjects = await JsonSerializer.DeserializeAsync<ProjectSerialization[]>(stream);

            if (lastProjects == null) return;
            var loadProjectTasks = new List<Task>();

            foreach (var l in lastProjects)
            {
                var manager = _projectManagerService.GetManager(l.ProjectType);
                if (manager != null)
                    loadProjectTasks.Add(LoadProjectAsync(l.Path, manager, l.IsExpanded, l.IsActive));
                else
                    ContainerLocator.Container.Resolve<ILogger>()?
                        .Warning(
                            $"Could not load project of type: {l.ProjectType}. No Manager Registered. Are you missing a plugin?");
            }

            await Task.WhenAll(loadProjectTasks);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public void RegisterConstructContextMenu(
        Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemModel>> construct)
    {
        _registerContextMenu.Add(construct);
    }

    private class ProjectSerialization
    {
        public ProjectSerialization(string projectType, string path, bool isExpanded, bool isActive)
        {
            ProjectType = projectType;
            Path = path;
            IsExpanded = isExpanded;
            IsActive = isActive;
        }

        public string ProjectType { get; }
        public string Path { get; }
        public bool IsExpanded { get; }
        public bool IsActive { get; }
    }

    #endregion
}