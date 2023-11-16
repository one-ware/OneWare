using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using OneWare.ProjectExplorer.Services;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Extensions;
using OneWare.Shared.Helpers;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.ProjectExplorer.ViewModels;

public class ProjectExplorerViewModel : ProjectViewModelBase, IProjectExplorerService
{
    public const string IconKey = "EvaIcons.FolderOutline";
    public IActive Active { get; }

    private readonly IPaths _paths;
    private readonly ISettingsService _settingsService;
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    private readonly IProjectManagerService _projectManagerService;
    private readonly IFileWatchService _fileWatchService;
    private readonly ILanguageManager _languageManager;

    private readonly string _lastProjectsFile;

    private Dictionary<string, IFile> TemporaryFiles { get; } = new();

    private IProjectRoot? _activeProject;

    private readonly List<Func<IList<IProjectEntry>, IEnumerable<IMenuItem>?>> _registerContextMenu = new();

    private IEnumerable<IMenuItem>? _treeViewContextMenu;

    public IEnumerable<IMenuItem>? TreeViewContextMenu
    {
        get => _treeViewContextMenu;
        set => SetProperty(ref _treeViewContextMenu, value);
    }

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

    public event EventHandler<IFile>? FileRemoved;
    public event EventHandler<IProjectRoot>? ProjectRemoved;

    public ProjectExplorerViewModel(IActive active, IPaths paths, IDockService dockService,
        IWindowService windowService, ISettingsService settingsService,
        IProjectManagerService projectManagerService, IFileWatchService fileWatchService,
        ILanguageManager languageManager)
        : base(IconKey)
    {
        Active = active;
        _paths = paths;
        _dockService = dockService;
        _windowService = windowService;
        _settingsService = settingsService;
        _projectManagerService = projectManagerService;
        _fileWatchService = fileWatchService;
        _languageManager = languageManager;

        _lastProjectsFile = Path.Combine(_paths.AppDataDirectory, "LastProjects.json");

        Id = "ProjectExplorer";
        Title = "Project Explorer";
    }

    public void DoubleTab(IProjectEntry entry)
    {
        if (entry is IProjectFile file)
        {
            _ = _dockService.OpenFileAsync(file);
        }
        else
        {
            entry.IsExpanded = !entry.IsExpanded;
        }
    }

    public void ConstructContextMenu(TopLevel topLevel)
    {
        var menuItems = new List<IMenuItem>();

        if (SelectedItem is { } entry)
        {
            var manager = _projectManagerService.GetManager(entry.Root.ProjectTypeId);

            switch (entry)
            {
                case IProjectFile file:
                    menuItems.Add(new MenuItemModel("Open")
                    {
                        Header = "Open",
                        Command = new RelayCommand(() => _dockService.OpenFileAsync(file))
                    });
                    break;
                case IProjectFolder folder:

                    if (folder is IProjectRoot { IsActive: false } inactiveRoot)
                    {
                        menuItems.Add(new MenuItemModel("SetActive")
                        {
                            Header = "Set as Active Project",
                            Command = new RelayCommand(() => ActiveProject = inactiveRoot),
                            ImageIconObservable = Application.Current!.GetResourceObservable("VsCodeLight.Debug-Start")
                        });
                    }

                    menuItems.Add(new MenuItemModel("Add")
                    {
                        Header = "Add",
                        Items = new List<IMenuItem>()
                        {
                            new MenuItemModel("NewFolder")
                            {
                                Header = "New Folder",
                                Command = new RelayCommand(() => _ = CreateFolderDialogAsync(folder)),
                                ImageIconObservable =
                                    Application.Current?.GetResourceObservable("VsImageLib.OpenFolder16X")
                            },
                            new MenuItemModel("NewFile")
                            {
                                Header = "New File",
                                Command = new RelayCommand(() => _ = CreateFileDialogAsync(folder)),
                                ImageIconObservable =
                                    Application.Current?.GetResourceObservable("VsImageLib.NewFile16X")
                            },
                            new MenuItemModel("ExistingFolder")
                            {
                                Header = "Import Folder",
                                Command = new RelayCommand(() => _ = ImportFolderDialogAsync(folder)),
                                ImageIconObservable =
                                    Application.Current?.GetResourceObservable("VsImageLib.Folder16X")
                            },
                            new MenuItemModel("ExistingFile")
                            {
                                Header = "Import File",
                                Command = new RelayCommand(() => _ = ImportFileDialogAsync(folder)),
                                ImageIconObservable =
                                    Application.Current?.GetResourceObservable("VsImageLib.File16X")
                            }
                        }
                    });
                    break;
            }

            foreach (var reg in _registerContextMenu)
            {
                if(reg.Invoke(SelectedItems) is {} items) menuItems.AddRange(items);
            }

            if (entry is IProjectRoot root)
            {
                menuItems.Add(new MenuItemModel("Close")
                {
                    Header = "Close",
                    Command = new AsyncRelayCommand(() => RemoveAsync(root))
                });
            }

            if (entry is not IProjectRoot)
            {
                menuItems.Add(new MenuItemModel("Edit")
                {
                    Header = "Edit",
                    Items = new List<IMenuItem>()
                    {
                        // new MenuItemModel("Cut")
                        // {
                        //     Header = "Cut",
                        //     Command = new RelayCommand(() => _ = CutAsync(entry)),
                        //     ImageIconObservable = Application.Current?.GetResourceObservable("MaterialDesign.DeleteForever")
                        // },
                        new MenuItemModel("Copy")
                        {
                            Header = "Copy",
                            Command = new RelayCommand(() => _ = CopyAsync(topLevel)),
                            ImageIconObservable = Application.Current?.GetResourceObservable("BoxIcons.RegularCopy"),
                            InputGesture = new KeyGesture(Key.C, KeyModifiers.Control),
                        },
                        new MenuItemModel("Paste")
                        {
                            Header = "Paste",
                            Command = new RelayCommand(() => _ = PasteAsync(topLevel)),
                            ImageIconObservable = Application.Current?.GetResourceObservable("BoxIcons.RegularPaste"),
                            InputGesture = new KeyGesture(Key.V, KeyModifiers.Control),
                        },
                        new MenuItemModel("Delete")
                        {
                            Header = "Delete",
                            Command = new RelayCommand(() => _ = DeleteDialogAsync(entry)),
                            ImageIconObservable =
                                Application.Current?.GetResourceObservable("MaterialDesign.DeleteForever")
                        },
                        new MenuItemModel("Rename")
                        {
                            Header = "Rename",
                            Command = new RelayCommand(() =>
                                entry.RequestRename?.Invoke((x) => _ = RenameAsync(entry, x))),
                            ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.Rename16X")
                        }
                    }
                });
            }

            menuItems.Add(new MenuItemModel("OpenFileViewer")
            {
                Header = "Open in File Viewer",
                Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(entry.FullPath)),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.OpenFolder16Xc")
            });
        }

        TreeViewContextMenu = menuItems;
    }

    public async Task OpenFileDialogAsync()
    {
        var file = await StorageProviderHelper.SelectFileAsync(_dockService.GetWindowOwner(this)!, "Select File", null);

        if (file != null)
        {
            await _dockService.OpenFileAsync(GetTemporaryFile(file));
        }
    }

    public IFile GetTemporaryFile(string path)
    {
        if (TemporaryFiles.TryGetValue(path, out var file)) return file;
        var externalFile = new ExternalFile(path);
        _fileWatchService.Register(externalFile);
        TemporaryFiles.Add(path, externalFile);
        return TemporaryFiles[path];
    }

    public void RemoveTemporaryFile(IFile file)
    {
        TemporaryFiles.Remove(file.FullPath);
        _fileWatchService.Unregister(file);
        FileRemoved?.Invoke(this, file);
    }

    public override void Insert(IProjectEntry entry)
    {
        base.Insert(entry);
        if (entry is IProjectRoot root)
        {
            _fileWatchService.Register(root);
            _languageManager.AddProject(root);
        }
    }

    private async Task<bool> AskForIncludeDialogAsync(IProjectRoot root, string relativePath)
    {
        if (!root.IsPathIncluded(relativePath))
        {
            var dialogResult = await _windowService.ShowYesNoCancelAsync("Warning",
                $"{Path.GetFileName(relativePath)} is not included in {root.Header}! Do you want to include it?",
                MessageBoxIcon.Warning, _dockService.GetWindowOwner(this));

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

    public async Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager)
    {
        var folderPath = await StorageProviderHelper.SelectFolderAsync(
            _dockService.GetWindowOwner(this) ?? throw new NullReferenceException("Window"), "Select Folder Path",
            _paths.ProjectsDirectory);

        if (folderPath == null) return null;

        var result = await LoadProjectAsync(folderPath, manager);

        _ = SaveLastProjectsFileAsync();

        return result;
    }

    public async Task<IProjectRoot?> LoadProjectFileDialogAsync(IProjectManager manager,
        params FilePickerFileType[]? filters)
    {
        var filePath = await StorageProviderHelper.SelectFileAsync(
            _dockService.GetWindowOwner(this) ?? throw new NullReferenceException("Window"), "Select Project File",
            _paths.ProjectsDirectory, filters);

        if (filePath == null) return null;

        var result = await LoadProjectAsync(filePath, manager);

        _ = SaveLastProjectsFileAsync();

        return result;
    }

    public async Task<IProjectRoot?> LoadProjectAsync(string path, IProjectManager manager, bool expand = true)
    {
        var project = await manager.LoadProjectAsync(path);

        if (project == null) return null;

        if (expand) project.IsExpanded = expand;

        Insert(project);
        ActiveProject = project;

        return project;
    }

    #region Create

    public async Task CreateFileDialogAsync(IProjectFolder parent)
    {
        var newFile = await _windowService.ShowInputAsync("Create File", "Enter a name for the new file!",
            MessageBoxIcon.Info,
            "NewFile.txt", _dockService.GetWindowOwner(this));

        if (!string.IsNullOrWhiteSpace(newFile))
        {
            parent.IsExpanded = true;
            if (!parent.Root.IsPathIncluded(newFile))
            {
                parent.Root.IncludePath(newFile);
                _ = SaveProjectAsync(parent.Root);
            }
            var f = parent.AddFile(newFile, true);
            await _dockService.OpenFileAsync(f);
        }
    }

    public async Task CreateFolderDialogAsync(IProjectFolder parent)
    {
        var newFolder = await _windowService.ShowInputAsync("Create Folder", "Enter a name for the new folder!",
            MessageBoxIcon.Info,
            "NewFolder", _dockService.GetWindowOwner(this));

        if (!string.IsNullOrWhiteSpace(newFolder))
        {
            parent.IsExpanded = true;
            parent.AddFolder(newFolder, true);
        }
    }

    #endregion

    #region Remove and Delete

    public async Task RemoveAsync(params IProjectEntry[] entries)
    {
        var roots = entries.Where(x => x.Root != null)
            .Select(x => x.Root)
            .Distinct()
            .ToList();

        foreach (var entry in entries) await RemoveAsync(entry);

        roots.RemoveAll(x => !Items.Contains(x));

        //await Task.WhenAll(roots.Select(x => ProjectManager.SaveAsync(x)));
    }

    private async Task RemoveAsync(IProjectEntry entry)
    {
        if (entry is IProjectRoot proj)
        {
            var openFiles = _dockService.OpenFiles.Where(x => x.Key is IProjectFile pf && pf.Root == proj).ToList();

            foreach (var tab in openFiles)
            {
                if (!await _dockService.CloseFileAsync(tab.Key)) return;
            }

            ProjectRemoved?.Invoke(this, proj);
            _fileWatchService.Unregister(proj);
            _languageManager.RemoveProject(proj);

            var activeProj = proj == ActiveProject;

            Items.Remove(proj);

            if (Items.Count == 0) //Avalonia bugfix
            {
                SelectedItems.Clear();
            }

            if (activeProj)
                ActiveProject = Items.Count > 0 ? Items[0] as IProjectRoot : null;

            return;
        }
        else if (entry is IProjectFile file)
        {
            if (!await _dockService.CloseFileAsync(file)) return;
            FileRemoved?.Invoke(this, file);
        }

        if (entry.TopFolder == null) throw new NullReferenceException(entry.Header + " has no TopFolder");

        entry.TopFolder.Remove(entry);
    }

    public async Task DeleteDialogAsync(params IProjectEntry[] entries)
    {
        var message = "Are you sure you want to delete this file permanently?";
        if (entries.Length == 1)
        {
            if (entries[0] is IProjectRoot)
                message =
                    "Are you sure you want to delete this project permanently? This will also delete all included files and folders";
            else if (entries[0] is IProjectFolder)
                message = "Are you sure you want to delete this folder permanently?";
        }
        else
        {
            message = "Are you sure you want to delete selected files?";
        }

        var result = await _windowService.ShowYesNoAsync("Warning", message, MessageBoxIcon.Warning,
            _dockService.GetWindowOwner(this));

        if (result == MessageBoxStatus.No) return;

        await DeleteAsync(entries);
    }

    public async Task DeleteAsync(params IProjectEntry[] entries)
    {
        foreach (var entry in entries) await DeleteAsync(entry);
    }

    private async Task DeleteAsync(IProjectEntry entry)
    {
        try
        {
            if (entry is IProjectRoot root)
            {
                foreach (var item in root.Items.ToList())
                    await DeleteAsync(item);
            }
            else if (entry is IProjectFolder folder)
                Directory.Delete(folder.FullPath, true);
            else
                File.Delete(entry.FullPath);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Error("File / Directory could not be deleted from storage!" + e);
        }

        await RemoveAsync(new[] { entry });
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

        var folders = await StorageProviderHelper.SelectFoldersAsync(_dockService.GetWindowOwner(this)!,
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

        var files = await StorageProviderHelper.SelectFilesAsync(_dockService.GetWindowOwner(this)!,
            "Import Files to " + destination.Header,
            destination.FullPath);

        await ImportAsync(destination, true, true, files.ToArray());
    }

    public async Task ImportAsync(IProjectFolder destination, bool copy, bool askForInclude, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (path == destination.FullPath) continue;

            try
            {
                var attr = File.GetAttributes(path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    var destPath = Path.Combine(destination.FullPath, Path.GetFileName(path)).CheckNameDirectory();
                    if (askForInclude)
                        if(!await AskForIncludeDialogAsync(destination.Root,
                            Path.GetRelativePath(destination.Root.FullPath, destPath))) return;
                    if (copy) PlatformHelper.CopyDirectory(path, destPath);
                    else Directory.Move(path, destPath);
                }
                else
                {
                    var destPath = Path.Combine(destination.FullPath, Path.GetFileName(path)).CheckNameFile();
                    if (askForInclude)
                        if(!await AskForIncludeDialogAsync(destination.Root,
                               Path.GetRelativePath(destination.Root.FullPath, destPath))) return;
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

    public async Task<IProjectEntry> RenameAsync(IProjectEntry entry, string newName)
    {
        var oldName = entry.Header;
        newName = newName.Trim();
        if (newName == oldName) return entry;
        var pathBase = Path.GetDirectoryName(entry.FullPath);

        if (!newName.IsValidFileName() || (entry is IProjectFolder && Path.HasExtension(newName)) ||
            pathBase == null || entry.TopFolder == null)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error($"Can't rename {entry.Header} to {newName}!");
            return entry;
        }

        var oldPath = entry.FullPath;
        var newPath = Path.Combine(pathBase, newName);

        try
        {
            if (entry is IProjectFile file)
            {
                if (File.Exists(newPath)) throw new Exception($"File {newPath} does already exist!");
                File.Move(oldPath, newPath);
                file.LastSaveTime = DateTime.Now;
            }
            else if (entry is IProjectFolder folder)
            {
                if (Directory.Exists(newPath)) throw new Exception($"Folder {newPath} does already exist!");
                Directory.Move(oldPath, newPath);
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
        
        return entry;
    }

    public async Task<IProjectEntry> ReloadAsync(IProjectEntry entry)
    {
        if (!entry.IsValid())
        {
            ContainerLocator.Container.Resolve<ILogger>()
                .Error("Tried to reload invalid entry (no root) " + entry.Header);
            return entry;
        }

        if (entry is IProjectRoot root)
        {
            var manager = _projectManagerService.GetManager(root.ProjectTypeId);

            if (manager == null)
            {
                entry.LoadingFailed = true;
                ContainerLocator.Container.Resolve<ILogger>()
                    .Error($"Cannot reload {entry.Header}. Manager not found!");
            }

            var proj = manager != null ? await manager.LoadProjectAsync(root.ProjectPath) : null;
            if (proj == null)
            {
                entry.LoadingFailed = true;
                return entry;
            }

            var expanded = root.IsExpanded;
            var active = root.IsActive;
            await RemoveAsync(root);
            Insert(proj);
            proj.IsExpanded = expanded;
            if (active) ActiveProject = proj;
            return proj;
        }

        if (entry.TopFolder == null) return entry;

        if (entry is IProjectFolder folder)
        {
            //TODO
            return entry;
        }

        if (entry is IProjectFile file)
        {
            _dockService.OpenFiles.TryGetValue(file, out var evm);
            if (evm is not null)
            {
                evm.FullPath = file.FullPath;
                evm.InitializeContent();
            }

            return file;
        }

        throw new Exception("Unknown filetype");
    }

    public Task SaveProjectAsync(IProjectRoot project)
    {
        var manager = _projectManagerService.GetManager(project.ProjectTypeId);
        if (manager == null) throw new NullReferenceException(nameof(manager));
        return manager.SaveProjectAsync(project);
    }

    #endregion

    #region Copy and Paste, Drop Files

    public async Task DropAsync(IProjectFolder destination, bool warning, bool copy, params string[] paths)
    {
        if (warning)
        {
            var action = (copy ? "copy" : "move");
            var result = await _windowService.ShowYesNoAsync("Warning",
                $"Are you sure you want to {action} {paths.Length} objects?", MessageBoxIcon.Warning);
            if(result is not MessageBoxStatus.Yes) return;
        }
        await ImportAsync(destination, copy, true, paths);
        destination.IsExpanded = true;
    }

    public async Task CopyAsync(TopLevel topLevel)
    {
        if (topLevel.Clipboard is not { } clipboard) return;

        var dataObject = await GetDataObjectFromItemsAsync(topLevel, SelectedItems);

        if (dataObject == null) return;
        await clipboard.SetDataObjectAsync(dataObject);
    }

    private const string CustomFileCopyFormat = "application/oneware-projectexplorer-copy";

    private static async Task<DataObject?> GetDataObjectFromItemsAsync(TopLevel topLevel,
        IEnumerable<IProjectEntry> items)
    {
        var dataObject = new DataObject();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var files = (await items
                    .SelectAsync(async x => await topLevel.StorageProvider.TryGetFileFromPathAsync(x.FullPath)))
                .Where(x => x != null)
                .Cast<IStorageFile>()
                .ToArray();

            if (!files.Any()) return null;

            dataObject.Set(DataFormats.Files, files);
        }
        else
        {
            dataObject.Set(CustomFileCopyFormat, string.Join('|', items.Select(x => x.FullPath)));
        }

        return dataObject;
    }

    public async Task PasteAsync(TopLevel topLevel)
    {
        if (topLevel.Clipboard is not { } clipboard) return;

        var target = SelectedItem as IProjectFolder ?? SelectedItem?.TopFolder;
        if (target == null) return;

        var formats = await clipboard.GetFormatsAsync();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var files = await clipboard.GetDataAsync(DataFormats.Files);
            if (files is IEnumerable<IStorageItem> storageItems)
            {
                await ImportAsync(target, true, true, storageItems
                    .Select(x => x.TryGetLocalPath())
                    .Where(x => x != null)
                    .Cast<string>().ToArray());
            }
        }
        else
        {
            var copyContext = await clipboard.GetDataAsync(CustomFileCopyFormat);
            if (copyContext is byte[] { Length: > 0 } byteArray)
            {
                var str = Encoding.Default.GetString(byteArray);
                var files = str.Split('|');

                await ImportAsync(target, true, true, files);
            }
        }
        target.IsExpanded = true;
    }

    #endregion

    #region LastProjectsFile

    public async Task SaveLastProjectsFileAsync()
    {
        try
        {
            var roots = Items.Where(x => x is IProjectRoot).Cast<IProjectRoot>();
            var serialization = roots
                .Select(x => new ProjectSerialization(x.ProjectTypeId, x.ProjectPath, x.IsExpanded)).ToArray();
            await using var stream = File.OpenWrite(_lastProjectsFile);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, serialization);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public async Task OpenLastProjectsFileAsync()
    {
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
                {
                    loadProjectTasks.Add(LoadProjectAsync(l.Path, manager, l.IsExpanded));
                }
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

    public void RegisterConstructContextMenu(Func<IList<IProjectEntry>, IEnumerable<IMenuItem>?> construct)
    {
        _registerContextMenu.Add(construct);
    }

    private class ProjectSerialization
    {
        public string ProjectType { get; set; }
        public string Path { get; set; }
        public bool IsExpanded { get; set; }

        public ProjectSerialization(string projectType, string path, bool isExpanded)
        {
            ProjectType = projectType;
            Path = path;
            IsExpanded = isExpanded;
        }
    }

    #endregion
}