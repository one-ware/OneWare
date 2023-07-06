using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Extensions;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.ProjectExplorer.ViewModels;

public class ProjectExplorerViewModel : ProjectViewModelBase, IProjectExplorerService
{
    public IActive Active { get; }

    private readonly IPaths _paths;
    private readonly ISettingsService _settingsService;
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    private readonly IProjectManagerService _projectManagerService;

    private Dictionary<string, IFile> TemporaryFiles { get; } = new();

    private IProjectRoot? _activeProject;

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

    public ProjectExplorerViewModel(IActive active, IPaths paths, IDockService dockService, IWindowService windowService, ISettingsService settingsService, IProjectManagerService projectManagerService)
    {
        Active = active;
        _paths = paths;
        _dockService = dockService;
        _windowService = windowService;
        _settingsService = settingsService;
        _projectManagerService = projectManagerService;

        Id = "ProjectExplorer";
        Title = "Project Explorer";
    }

    public void ConstructContextMenu()
    {
        var menuItems = new List<IMenuItem>();

        if (SelectedItem is { } entry)
        {
            if (entry is IProjectFile file)
            {
                menuItems.Add(new MenuItemModel("Open")
                {
                    Header = "Open",
                    Command = new RelayCommand(() => _dockService.OpenFileAsync(file))
                });
            }
            else if (entry is IProjectFolder folder)
            {
                menuItems.Add(new MenuItemModel("Add")
                {
                    Header = "Add",
                    Items = new List<IMenuItem>()
                    {
                        new MenuItemModel("NewFolder")
                        {
                            Header = "New Folder",
                            Command = new RelayCommand(() => folder.AddFolder("NewFolder", true)),
                            ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.OpenFolder16X")
                        },
                        new MenuItemModel("NewFile")
                        {
                            Header = "New File",
                            Command = new RelayCommand(() => folder.AddFile("NewFile", true)),
                            ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.NewFile16X")
                        }
                    }
                });
            }
            if(entry is not IProjectRoot) menuItems.Add(new MenuItemModel("Rename")
            {
                Header = "Rename",
                Command = new RelayCommand(() => entry.RequestRename?.Invoke((x) => _ = RenameAsync(entry, x))),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.Rename16X")
            });
            menuItems.Add(new MenuItemModel("OpenFileViewer")
            {
                Header = "Open in File Viewer",
                Command = new RelayCommand(() => Tools.OpenExplorerPath(entry.FullPath)),
                ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.OpenFolder16Xc")
            });
        }

        TreeViewContextMenu = menuItems;
    }
    
    public async Task OpenFileDialogAsync()
    {
        var file = await Tools.SelectFileAsync(_dockService.GetWindowOwner(this), "Select File", null);

        if (file != null)
        {
            await _dockService.OpenFileAsync(GetTemporaryFile(file));
        }
    }

    public IFile GetTemporaryFile(string path)
    {
        if (TemporaryFiles.TryGetValue(path, out var file)) return file;
        TemporaryFiles.Add(path, new ExternalFile(path));
        return TemporaryFiles[path];
    }

    public void RemoveTemporaryFile(IFile file)
    {
        TemporaryFiles.Remove(file.FullPath);
    }

    public async Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager)
    {
        var folderPath = await Tools.SelectFolderAsync(_dockService.GetWindowOwner(this) ?? throw new NullReferenceException("Window"), "Select Folder Path",
            _paths.ProjectsDirectory);

        if (folderPath == null) return null;

        return await LoadProjectAsync(folderPath, manager);
    }
    
    public async Task<IProjectRoot?> LoadProjectFileDialogAsync(IProjectManager manager)
    {
        var filePath = await Tools.SelectFileAsync(_dockService.GetWindowOwner(this) ?? throw new NullReferenceException("Window"), "Select Project File",
            _paths.ProjectsDirectory);

        if (filePath == null) return null;

        return await LoadProjectAsync(filePath, manager);
    }

    public async Task<IProjectRoot?> LoadProjectAsync(string path, IProjectManager manager)
    {
        var project = await manager.LoadProjectAsync(path);
        
        if (project == null) return null;
        
        project.IsExpanded = true;
        
        Insert(project);
        ActiveProject = project;
        
        //project.SetupFileWatcher();
        return project;
    }

    private async Task RemoveAsync(params IProjectEntry[] entries)
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
                if(!await _dockService.CloseFileAsync(tab.Key)) return;
            }
            
            proj.Cleanup();

            var activeProj = proj == ActiveProject;
                
            Items.Remove(proj);

            if (Items.Count == 0) //Avalonia bug fix
            {
                SelectedItems.Clear();
            }

            if(activeProj)
                ActiveProject = Items.Count > 0 ? Items[0] as IProjectRoot : null;
            
            return;
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
                message = "Are you sure you want to delete this project permanently? This will also delete all included files and folders";
            else if (entries[0] is IProjectFolder)
                message = "Are you sure you want to delete this folder permanently?";
        }
        else
        {
            message = "Are you sure you want to delete selected files?";
        }

        var result = await _windowService.ShowYesNoAsync("Warning", message, MessageBoxIcon.Warning, _dockService.GetWindowOwner(this));
        
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
                root.Cleanup();
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
            ContainerLocator.Container.Resolve<ILogger>()?.Error("File / Directory could not be deleted from storage!" + e);
        }

        await RemoveAsync(new []{entry});

            
    }

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
            
        var paths = await Tools.SelectFoldersAsync(_dockService.GetWindowOwner(this), "Import Folders to " + destination.Header,
            destination.FullPath);

        foreach (var path in paths)
        {
            var folder = destination.AddFolder(Path.GetFileName(path));
            ImportFolderRecursive(path, folder);
        }
            
        //await destination.Root.SaveProjectAsync();
        //await destination.Root.ResolveAsync();
    }

    public void ImportFolderRecursive(string source, IProjectFolder destination, params string[] exclude)
    {
        var entries = Directory.GetFileSystemEntries(source);
        foreach (var entry in entries)
        {
            var attr = File.GetAttributes(entry);
            if (attr.HasFlag(FileAttributes.Hidden)) continue;
            if (exclude.Contains(Path.GetFileName(entry)) || exclude.Contains(entry)) continue;

            if (attr.HasFlag(FileAttributes.Directory))
            {
                if(entry.EqualPaths(destination.FullPath)) continue;
                var folder = destination.AddFolder(Path.GetFileName(entry));
                ImportFolderRecursive(entry, folder);
            }
            else
            {
                ImportFile(entry, destination);
            }
        }
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

        var files = await Tools.SelectFilesAsync(_dockService.GetWindowOwner(this), "Import Files to " + destination.Header,
            destination.FullPath);

        if (!files.Any()) return;
            
        //After Dialog closes
        foreach (var path in files) ImportFile(path, destination);

        //await destination.Root.SaveProjectAsync();
        //await destination.Root.ResolveAsync();
    }

    private static void ImportFile(string source, IProjectFolder top, bool overwrite = false)
    {
        //Format Path
        source = Path.GetFullPath(source);
        var destination = Path.Combine(top.FullPath, Path.GetFileName(source));

        //Check if File exists
        if (!File.Exists(source))
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Warning($"Cannot import {source}. File does not exist");
            return;
        }
        try
        {
            if(!source.IsSamePathAs(destination))
                Tools.CopyFile(source, destination, overwrite);

            top.AddFile(Path.GetFileName(destination));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public async Task<IProjectEntry> RenameAsync(IProjectEntry entry, string newName)
    {
        var oldName = entry.Header;
        newName = newName.Trim();
        if (newName == oldName) return entry;
        var pathBase = Path.GetDirectoryName(entry.FullPath);
        
        if (!Tools.IsValidFileName(newName) || (entry is IProjectFolder && Path.HasExtension(newName)) || pathBase == null || entry.TopFolder == null)
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

        (entry as IProjectEntry)!.Header = newName;

        await ReloadAsync(entry);

        //if (entry.HasRoot)
        //{
            //_ = entry.Root.SaveProjectAsync(); TODO
        //}

        return entry;
    }
    
    public async Task<IProjectEntry> ReloadAsync(IProjectEntry entry)
    {
        if (!entry.IsValid())
        {
            ContainerLocator.Container.Resolve<ILogger>().Error("Tried to reload invalid entry (no root) " + entry.Header);
            return entry;
        }

        if (entry is IProjectRoot)
        {
            await RemoveAsync(entry);
            var proj = await _projectManagerService.GetManager(entry.GetType()).LoadProjectAsync(entry.FullPath);
            if (proj == null)
            {
                entry.LoadingFailed = true;
                return entry;
            }
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
            if (evm is IEditor editor)
            {
                editor.FullPath = file.FullPath;
            }
            evm?.InitializeContent();
            return file;
        }

        throw new Exception("Unknown filetype");
    }

    public async Task HandleFileChangeAsync(string path)
    {
        try
        {
            await Task.Delay(10);
                
            //TODO await MainDock.SourceControl.WaitUntilFreeAsync();

            //if (MainDock.OpenComparisons.ContainsKey(fullPath))
            //    MainDock.SourceControl.Compare(fullPath, false); //Or check for violation

            if (Search(path) is {} entry) //NOT Ignored
                if (entry is IFile file)
                {
                    var fileDate = File.GetLastWriteTime(path);
                    if (file.LastSaveTime >= fileDate) return;
                    
                    var fileOpen = _dockService.OpenFiles.ContainsKey(file);
                        
                    if (!fileOpen) return;
                       
                    if (fileOpen && _settingsService.GetSettingValue<bool>("Editor_NotifyExternalChanges"))
                    {
                        var result = await _windowService.ShowYesNoAsync("Warning",
                            $"{entry.RelativePath} has been modified by another program. Would you like to reload it?",
                            MessageBoxIcon.Warning, _dockService.GetWindowOwner(this));
                        if (result != MessageBoxStatus.Yes) return;
                    }
                    else
                    {
                        await Task.Delay(100);
                    }

                    _ = ReloadAsync(entry);
                }
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(ex.Message, ex);
        }
    }
}