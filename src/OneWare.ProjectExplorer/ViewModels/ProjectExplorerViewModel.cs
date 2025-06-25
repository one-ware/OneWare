using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ProjectExplorer.Services;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.ViewModels;

public class ProjectExplorerViewModel : ProjectViewModelBase, IProjectExplorerService
{
    public const string IconKey = "EvaIcons.FolderOutline";
    private readonly IDockService _dockService;
    private readonly IFileWatchService _fileWatchService;
    private readonly ILanguageManager _languageManager;

    private readonly string _lastProjectsFile;

    private readonly IPaths _paths;
    private readonly IProjectManagerService _projectManagerService;

    private readonly List<Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemViewModel>>> _registerContextMenu =
        new();

    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    private IProjectRoot? _activeProject;
    
    public ProjectExplorerViewModel(IApplicationStateService applicationStateService,
        IPaths paths,
        IDockService dockService,
        IWindowService windowService,
        ISettingsService settingsService,
        IProjectManagerService projectManagerService,
        IFileWatchService fileWatchService,
        ILanguageManager languageManager)
        : base(IconKey)
    {
        ApplicationStateService = applicationStateService;
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

    public IApplicationStateService ApplicationStateService { get; }

    private Dictionary<string, IFile> TemporaryFiles { get; } = new();

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

    public override void Insert(IProjectRoot project)
    {
        base.Insert(project);
        _fileWatchService.Register(project);
        _languageManager.AddProject(project);
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

    public async Task<IProjectRoot?> LoadProjectAsync(string path, IProjectManager manager, bool expand = true,
        bool setActive = true)
    {
        var project = await manager.LoadProjectAsync(path);

        if (project == null) return null;

        if (expand) project.IsExpanded = expand;

        Insert(project);

        if (setActive) ActiveProject = project;

        return project;
    }

    public void DoubleTab(IProjectEntry entry)
    {
        if (entry is IProjectFile file)
            _ = _dockService.OpenFileAsync(file);
        else
            entry.IsExpanded = !entry.IsExpanded;
    }

    public void ConstructContextMenu(TopLevel topLevel)
    {
        var menuItems = new List<MenuItemViewModel>();

        if (SelectedItems is [{ } item])
        {
            switch (item)
            {
                case IProjectFile file:
                    menuItems.Add(new MenuItemViewModel("Open")
                    {
                        Header = "Open",
                        Command = new AsyncRelayCommand(() => _dockService.OpenFileAsync(file))
                    });
                    break;
                case IProjectFolder folder:

                    if (folder is IProjectRoot { IsActive: false } inactiveRoot)
                        menuItems.Add(new MenuItemViewModel("SetActive")
                        {
                            Header = "Set as Active Project",
                            Command = new RelayCommand(() => ActiveProject = inactiveRoot),
                            IconObservable = Application.Current!.GetResourceObservable("VsCodeLight.Debug-Start")
                        });

                    menuItems.Add(new MenuItemViewModel("Add")
                    {
                        Header = "Add",
                        Items = new ObservableCollection<MenuItemViewModel>
                        {
                            new("NewFolder")
                            {
                                Header = "New Folder",
                                Command = new RelayCommand(() => _ = CreateFolderDialogAsync(folder)),
                                IconObservable =
                                    Application.Current!.GetResourceObservable("VsImageLib.OpenFolder16X")
                            },
                            new("NewFile")
                            {
                                Header = "New File",
                                Command = new RelayCommand(() => _ = CreateFileDialogAsync(folder)),
                                IconObservable =
                                    Application.Current!.GetResourceObservable("VsImageLib.NewFile16X")
                            },
                            new("ExistingFolder")
                            {
                                Header = "Import Folder",
                                Command = new RelayCommand(() => _ = ImportFolderDialogAsync(folder)),
                                IconObservable =
                                    Application.Current!.GetResourceObservable("VsImageLib.Folder16X")
                            },
                            new("ExistingFile")
                            {
                                Header = "Import File",
                                Command = new RelayCommand(() => _ = ImportFileDialogAsync(folder)),
                                IconObservable =
                                    Application.Current!.GetResourceObservable("VsImageLib.File16X")
                            }
                        }
                    });
                    break;
            }

            foreach (var reg in _registerContextMenu) reg.Invoke(SelectedItems, menuItems);

            if (item is IProjectRoot root)
                menuItems.Add(new MenuItemViewModel("Close")
                {
                    Header = "Close",
                    Command = new AsyncRelayCommand(() => RemoveAsync(root))
                });

            if (item is IProjectEntry entry)
            {
                if (item is not IProjectRoot)
                    menuItems.Add(new MenuItemViewModel("Edit")
                    {
                        Header = "Edit",
                        Items = new ObservableCollection<MenuItemViewModel>
                        {
                            // new MenuItemModel("Cut")
                            // {
                            //     Header = "Cut",
                            //     Command = new RelayCommand(() => _ = CutAsync(entry)),
                            //     ImageIconObservable = Application.Current?.GetResourceObservable("MaterialDesign.DeleteForever")
                            // },
                            new("Copy")
                            {
                                Header = "Copy",
                                Command = new RelayCommand(() => _ = CopyAsync(topLevel)),
                                IconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularCopy"),
                                InputGesture = new KeyGesture(Key.C, KeyModifiers.Control)
                            },
                            new("Paste")
                            {
                                Header = "Paste",
                                Command = new RelayCommand(() => _ = PasteAsync(topLevel)),
                                IconObservable = Application.Current!.GetResourceObservable("BoxIcons.RegularPaste"),
                                InputGesture = new KeyGesture(Key.V, KeyModifiers.Control)
                            },
                            new("Delete")
                            {
                                Header = "Delete",
                                Command = new RelayCommand(() => _ = DeleteDialogAsync(entry)),
                                IconObservable =
                                    Application.Current!.GetResourceObservable("MaterialDesign.DeleteForever")
                            },
                            new("Rename")
                            {
                                Header = "Rename",
                                Command = new RelayCommand(() =>
                                    entry.RequestRename?.Invoke(x => _ = RenameAsync(entry, x))),
                                IconObservable = Application.Current!.GetResourceObservable("VsImageLib.Rename16X")
                            }
                        }
                    });

                menuItems.Add(new MenuItemViewModel("OpenFileViewer")
                {
                    Header = "Open in File Manager",
                    Command = new RelayCommand(() => PlatformHelper.OpenExplorerPath(entry.FullPath)),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.OpenFolder16Xc")
                });
            }
        }
        else if (SelectedItems.Count > 1)
        {
            if (SelectedItems.All(x => x is IProjectEntry and not IProjectRoot))
                menuItems.Add(new MenuItemViewModel("Edit")
                {
                    Header = "Edit",
                    Items = new ObservableCollection<MenuItemViewModel>
                    {
                        // new MenuItemModel("Cut")
                        // {
                        //     Header = "Cut",
                        //     Command = new RelayCommand(() => _ = CutAsync(entry)),
                        //     ImageIconObservable = Application.Current?.GetResourceObservable("MaterialDesign.DeleteForever")
                        // },
                        new("Delete")
                        {
                            Header = "Delete",
                            Command = new RelayCommand(() =>
                                _ = DeleteDialogAsync(SelectedItems.Cast<IProjectEntry>().ToArray())),
                            IconObservable =
                                Application.Current!.GetResourceObservable("MaterialDesign.DeleteForever")
                        }
                    }
                });


            foreach (var reg in _registerContextMenu) reg.Invoke(SelectedItems, menuItems);
        }

        TreeViewContextMenu = menuItems;
    }

    public async Task OpenFileDialogAsync()
    {
        var file = await StorageProviderHelper.SelectFileAsync(_dockService.GetWindowOwner(this)!, "Select File", null);

        if (file != null) await _dockService.OpenFileAsync(GetTemporaryFile(file));
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

    #region Create

    public async Task CreateFileDialogAsync(IProjectFolder parent)
    {
        var newFile = await _windowService.ShowInputAsync("Create File", "Enter a name for the new file!",
            MessageBoxIcon.Info,
            "NewFile.txt", _dockService.GetWindowOwner(this));

        if (!string.IsNullOrWhiteSpace(newFile))
        {
            if (!parent.Root.IsPathIncluded(newFile))
            {
                parent.Root.IncludePath(newFile);
                _ = SaveProjectAsync(parent.Root);
            }

            var f = parent.AddFile(newFile, true);
            parent.IsExpanded = true;
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

        roots.RemoveAll(x => !Projects.Contains(x));

        //await Task.WhenAll(roots.Select(x => ProjectManager.SaveAsync(x)));
    }

    private async Task RemoveAsync(IProjectEntry entry)
    {
        if (entry is IProjectRoot proj)
        {
            var openFiles = _dockService.OpenFiles.Where(x => x.Key is IProjectFile pf && pf.Root == proj).ToList();

            foreach (var tab in openFiles)
                if (!await _dockService.CloseFileAsync(tab.Key))
                    return;

            ProjectRemoved?.Invoke(this, proj);
            _fileWatchService.Unregister(proj);
            _languageManager.RemoveProject(proj);

            var activeProj = proj == ActiveProject;

            Projects.Remove(proj);

            if (Projects.Count == 0) //Avalonia bugfix
                SelectedItems.Clear();

            if (activeProj)
                ActiveProject = Projects.Count > 0 ? Projects[0] : null;

            return;
        }

        if (entry is IProjectFolder folder)
        {
            await RemoveAsync(folder.Entities.ToArray());
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
        if(entries.Length < 1) return;
        
        var message = string.Empty;
        
        if (entries.Length == 1)
        {
            message = entries[0] switch
            {
                IProjectRoot => $"Are you sure you want to delete the project {entries[0].Header} permanently? This will also delete all included files and folders",
                IProjectFolder => $"Are you sure you want to delete the folder {entries[0].Header} permanently?",
                IProjectFile => $"Are you sure you want to delete {entries[0].Header} permanently?",
                _ => message
            };
        }
        else
        {
            message = $"Are you sure you want to delete {entries.Length} objects permanently?";
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
                foreach (var item in root.Entities.ToList())
                    await DeleteAsync(item);
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
            if (!copy && path == destination.FullPath) continue;

            try
            {
                var attr = File.GetAttributes(path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    var destPath = Path.Combine(destination.FullPath, Path.GetFileName(path)).CheckNameDirectory();
                    // if (askForInclude)
                    //     if (!await AskForIncludeDialogAsync(destination.Root,
                    //             Path.GetRelativePath(destination.Root.FullPath, destPath)))
                    //         return;
                    if (copy) PlatformHelper.CopyDirectory(path, destPath);
                    else Directory.Move(path, destPath);
                }
                else
                {
                    var destPath = Path.Combine(destination.FullPath, Path.GetFileName(path)).CheckNameFile();
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
                file.Name = newName;
            }
            else if (entry is IProjectFolder folder)
            {
                if (Directory.Exists(newPath)) throw new Exception($"Folder {newPath} does already exist!");
                Directory.Move(oldPath, newPath);
                folder.Name = newName;
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
            //TODO
            return entry;

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
    
    public async Task<bool> SaveOpenFilesForProjectAsync(IProjectRoot project)
    {
        var saveTasks = _dockService.OpenFiles.Where(x => x.Key is IProjectFile file && file.Root == project)
            .Select(x => x.Value.SaveAsync());
            
        var results = await Task.WhenAll(saveTasks);

        return results.All(x => x);
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

        var dataObject = await GetDataObjectFromItemsAsync(topLevel, SelectedItems);

        if (dataObject == null) return;
        await clipboard.SetDataObjectAsync(dataObject);
    }

    private const string CustomFileCopyFormat = "application/oneware-projectexplorer-copy";

    private static async Task<DataObject?> GetDataObjectFromItemsAsync(TopLevel topLevel,
        IEnumerable<IProjectExplorerNode> items)
    {
        var dataObject = new DataObject();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
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

            dataObject.Set(DataFormats.Files, storageItems);
        }
        else
        {
            dataObject.Set(CustomFileCopyFormat, string.Join('|', items
                .Where(x => x is IProjectEntry)
                .Cast<IProjectEntry>()
                .Select(x => x.FullPath)));
        }

        return dataObject;
    }

    public async Task PasteAsync(TopLevel topLevel)
    {
        if (topLevel.Clipboard is not { } clipboard || SelectedItems is not [{ } selectedItem]) return;

        var target = selectedItem as IProjectFolder ?? selectedItem?.Parent as IProjectFolder;
        if (target == null) return;

        var formats = await clipboard.GetFormatsAsync();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var files = await clipboard.GetDataAsync(DataFormats.Files);
            if (files is IEnumerable<IStorageItem> storageItems)
                await ImportAsync(target, true, true, storageItems
                    .Select(x => x.TryGetLocalPath())
                    .Where(x => x != null)
                    .Cast<string>().ToArray());
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

    public Task DeleteSelectedDialog()
    {
        if (SelectedItems.Count == 0 || SelectedItems.Any(x => x is not IProjectEntry)) return Task.CompletedTask;
        
        return this.DeleteDialogAsync(SelectedItems.Cast<IProjectEntry>().ToArray());
    }
    
    #endregion

    #region LastProjectsFile

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
        Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemViewModel>> construct)
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