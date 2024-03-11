using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DynamicData;
using DynamicData.Binding;
using OneWare.Essentials;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.ProjectSystem.Models;

public class ProjectFolder : ProjectEntry, IProjectFolder
{
    public ProjectFolder(string header, IProjectFolder? topFolder, bool defaultFolderAnimation = true) : base(header, topFolder)
    {
        if (defaultFolderAnimation)
        {
            IDisposable? iconDisposable = null;
            this.WhenValueChanged(x => x.IsExpanded).Subscribe(x =>
            {
                iconDisposable?.Dispose();
                if (!x)
                {
                    iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.Folder16X").Subscribe(y =>
                    {
                        Icon = y as IImage;
                    });
                }
                else
                {
                    iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.FolderOpen16X").Subscribe(y =>
                    {
                        Icon = y as IImage;
                    });
                }
            });
        }
    }

    private void Insert(IProjectEntry entry)
    {
        //Insert in correct posiion
        var inserted = false;
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is IProjectEntry && (entry is ProjectFolder && Children[i] is not ProjectFolder ||
                entry is ProjectFolder && Children[i] is ProjectFolder &&
                string.CompareOrdinal(entry.Header, Children[i].Header) <= 0 || //Insert if both are folders
                entry is not ProjectFolder && Children[i] is not ProjectFolder &&
                string.CompareOrdinal(entry.Header, Children[i].Header) <= 0)) //Insert if both are files
            {
                Children.Insert(i, entry);
                inserted = true;
                break;
            }
        }
            
        if (!inserted) Children.Add(entry);
        entry.TopFolder = this;
        
        _entities.Add(entry);
        (entry.Root as ProjectRoot)?.RegisterEntry(entry);
    }

    public void Add(IProjectEntry entry)
    {
        entry.TopFolder = this;
        Insert(entry);
    }

    public void Remove(IProjectEntry entry)
    {
        if (entry is ProjectFolder folder)
        {
            for (int i = 0; i < folder.Entities.Count; i++)
            {
                folder.Remove(folder.Entities[i]);
                i--;
            }
        }

        (entry.Root as ProjectRoot)?.UnregisterEntry(entry);
        Children.Remove(entry);
        _entities.Remove(entry);

        //Collapse folder if empty
        if (Children.Count == 0) IsExpanded = false;
    }

    public IProjectFile? ImportFile(string path, bool overwrite)
    {
        var destination = Path.Combine(FullPath, Path.GetFileName(path));

        //Check if File exists
        if (!File.Exists(path))
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Warning($"Cannot import {path}. File does not exist");
            return null;
        }
        try
        {
            if(!path.EqualPaths(destination))
                PlatformHelper.CopyFile(path, destination, overwrite);

            return AddFile(Path.GetFileName(destination));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return null;
        }
    }

    public IProjectFile AddFile(string path, bool createNew = false)
    {
        var split = path.LastIndexOf(Path.DirectorySeparatorChar);

        if (split > 0)
        {
            var folderPath = path[..split];
            var pf = AddFolder(folderPath);
            return pf.AddFile(path.Substring(split + 1, path.Length - split - 1), createNew);
        }
            
        if (!createNew && SearchName(path, false) is ProjectFile file)
        {
            return file;
        }
            
        var fullPath = Path.Combine(FullPath, path);

        if (createNew)
        {
            fullPath = fullPath.CheckNameFile();
            path = Path.GetFileName(fullPath);
        }
            
        var projFile = ConstructNewProjectFile(path, this); 
            
        if (!File.Exists(fullPath))
        {
            if (createNew)
            {
                try
                {
                    PlatformHelper.CreateFile(fullPath);
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                }
            }
            else
            {
                projFile.LoadingFailed = true;
            }
        }

        Insert(projFile);
        return projFile;
    }

    protected virtual IProjectFolder ConstructNewProjectFolder(string path, IProjectFolder topFolder)
    {
        return new ProjectFolder(path, topFolder);
    }
    
    protected virtual IProjectFile ConstructNewProjectFile(string path, IProjectFolder topFolder)
    {
        return new ProjectFile(path, topFolder);
    }
    
    public IProjectFolder AddFolder(string path, bool createNew = false)
    {
        var folderParts = path.Split(Path.DirectorySeparatorChar);
        if (folderParts.Length > 1)
        {
            if (SearchName(folderParts[0], false) is ProjectFolder existing)
                return existing.AddFolder(path.Remove(0, folderParts[0].Length + 1), createNew);

            var created = AddFolder(folderParts[0], createNew);
            return created.AddFolder(path.Remove(0, folderParts[0].Length + 1), createNew);
        }

        if (!createNew && SearchRelativePath(path, false) is ProjectFolder existingFolder)
        {
            return existingFolder;
        }

        var fullPath = Path.Combine(FullPath, path);
        if (createNew)
        {
            fullPath = fullPath.CheckNameDirectory();
            path = Path.GetFileName(fullPath);
        }
            
        if (!Directory.Exists(fullPath))
        {
            try
            {
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }

        var pf = ConstructNewProjectFolder(path, this);
        
        Insert(pf);
        return pf;
    }

    public IProjectEntry? SearchName(string path, bool recursive = true)
    {
        if (path.EqualPaths(Name))
            return this;
        
        foreach (var i in Entities)
        {
            if (path.Equals(i.Name, StringComparison.OrdinalIgnoreCase)) return i;
            if (recursive && i is ProjectFolder folder)
            {
                var pe = folder.SearchName(path);
                if (pe != null) return pe;
            }
        }

        return null;
    }

    public IProjectEntry? SearchRelativePath(string path, bool recursive = true)
    {
        if (path.EqualPaths(RelativePath))
            return this;
        
        foreach (var i in Entities)
        {
            if (path.EqualPaths(i.RelativePath)) return i;
            if (recursive && i is ProjectFolder folder)
            {
                var pe = folder.SearchRelativePath(path);
                if (pe != null) return pe;
            }
        }

        return null;
    }

    public IProjectEntry? SearchFullPath(string path, bool recursive = true)
    {
        if (path.EqualPaths(FullPath))
            return this;
        
        foreach (var i in Entities)
        {
            if (path.EqualPaths(i.FullPath)) return i;
            if (recursive && i is ProjectFolder folder)
            {
                var pe = folder.SearchFullPath(path);
                if (pe != null) return pe;
            }
        }

        return null;
    }
}