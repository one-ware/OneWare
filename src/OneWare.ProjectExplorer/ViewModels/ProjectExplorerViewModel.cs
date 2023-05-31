using OneWare.ProjectExplorer.Models;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.Extensions;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.ProjectExplorer.ViewModels;

public class ProjectExplorerViewModel : ProjectViewModelBase, IProjectService
{
    public IActive Active { get; }

    private readonly IPaths _paths;
    private readonly ISettingsService _settingsService;
    private readonly IDockService _dockService;
    private readonly IWindowService _windowService;
    private readonly string _lastProjectDataPath;

    private Dictionary<string, IFile> TemporaryFiles { get; } = new();

    private IProjectRoot? _activeProject;
    public IProjectRoot? ActiveProject
    {
        get => _activeProject;
        set => SetProperty(ref _activeProject, value);
    }

    public ProjectExplorerViewModel(IActive active, IPaths paths, IDockService dockService, IWindowService windowService, ISettingsService settingsService)
    {
        Active = active;
        _paths = paths;
        _dockService = dockService;
        _windowService = windowService;
        _settingsService = settingsService;
        _lastProjectDataPath = Path.Combine(paths.AppDataDirectory, "lastProjectData.xml");
        
        Id = "ProjectFiles";
        Title = "Project Explorer";
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

    public async Task<IProjectRoot?> LoadProjectAsync(string path)
    {
        // path = Path.GetFullPath(path); //FORMAT
        //
        // var project = await ProjectManager.LoadAsync(path);
        //
        // if (project == null) return null;
        //
        // project.IsExpanded = true;
        //
        // Insert(project);
        // ActiveProject = project;
        //
        // project.SetupFileWatcher();
        // return project;
        return null;
    }

    private async Task RemoveAsync(params IProjectEntry[] entries)
    {
        var roots = entries.Where(x => x.Root != null)
            .Select(x => x.Root)
            .Distinct()
            .Cast<ProjectRoot>()
            .ToList();
            
        foreach (var entry in entries) await RemoveAsync(entry);

        roots.RemoveAll(x => !Items.Contains(x));
            
        //await Task.WhenAll(roots.Select(x => ProjectManager.SaveAsync(x)));
    }

    private async Task RemoveAsync(ProjectEntry entry)
    {
        if (entry is ProjectRoot proj)
        {
            var openFiles = _dockService.OpenFiles.Where(x => x.Key is ProjectFile pf && pf.Root == proj).ToList();

            foreach (var tab in openFiles)
            {
                if(!await _dockService.CloseFileAsync(tab.Key)) return;
            }
            
            proj.DisposeFileWatcher();

            var activeProj = proj == ActiveProject;
                
            Items.Remove(proj);

            if (Items.Count == 0) //Avalonia bug fix
            {
                SelectedItems.Clear();
            }

            if(activeProj)
                ActiveProject = Items.Count > 0 ? Items[0] as ProjectRoot : null;
            
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
            if (entries[0] is ProjectRoot)
                message = "Are you sure you want to delete this project permanently? This will also delete all included files and folders";
            else if (entries[0] is ProjectFolder)
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
            if (entry is ProjectRoot root)
            {
                root.DisposeFileWatcher();
                foreach (var item in root.Items.ToList())
                    await DeleteAsync(item);
                    
                if(root.ProjectFileName != null) File.Delete(Path.Combine(root.RootFolderPath, root.ProjectFileName));
            }
            else if (entry is ProjectFolder folder)
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

    public async Task ImportFolderDialogAsync(ProjectFolder? destination = null)
    {
        destination ??= ActiveProject as ProjectFolder;

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

    private static void ImportFolderRecursive(string source, IProjectFolder destination, params string[] exclude)
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
            if (entry is ProjectFile file)
            {
                if (File.Exists(newPath)) throw new Exception($"File {newPath} does already exist!");
                File.Move(oldPath, newPath);
                file.LastSaveTime = DateTime.Now;
            }
            else if (entry is ProjectFolder folder)
            {
                if (Directory.Exists(newPath)) throw new Exception($"Folder {newPath} does already exist!");
                Directory.Move(oldPath, newPath);
                folder.LastSaveTime = DateTime.Now;
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }

        await ReloadAsync(entry, newName);

        //if (entry.HasRoot)
        //{
            //_ = entry.Root.SaveProjectAsync(); TODO
        //}

        return entry;
    }
    
    public async Task<IProjectEntry> ReloadAsync(IProjectEntry entry, string? newName = null)
    {
        if (!entry.IsValid())
        {
            ContainerLocator.Container.Resolve<ILogger>().Error("Tried to reload invalid entry (no root) " + entry.Header);
            return entry;
        }

        if (entry is ProjectRoot)
        {
            await RemoveAsync(entry);
            var proj = await LoadProjectAsync(entry.FullPath);
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
            var addTab = _dockService.OpenFiles.ContainsKey(file);
            var topFolder = entry.TopFolder;
            var expanded = topFolder.IsExpanded;
            entry.TopFolder.Remove(entry);
            file = topFolder.AddFile(newName ?? file.Header);
            topFolder.IsExpanded = expanded;
            if (addTab) _ = _dockService.OpenFileAsync(file);
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