using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DynamicData.Binding;
using OneWare.Shared;
using OneWare.Shared.Extensions;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectSystem.Models;

public class ProjectFolder : ProjectEntry, IProjectFolder
{
    public ProjectFolder(string header, IProjectFolder? topFolder) : base(header, topFolder)
    {
        if (GetType() == typeof(ProjectFolder))
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
        for (var i = 0; i < Items.Count; i++)
        {
            if (entry is ProjectFolder && Items[i] is not ProjectFolder ||
                entry is ProjectFolder && Items[i] is ProjectFolder &&
                string.CompareOrdinal(entry.Header, Items[i].Header) <= 0 || //Insert if both are folders
                entry is not ProjectFolder && Items[i] is not ProjectFolder &&
                string.CompareOrdinal(entry.Header, Items[i].Header) <= 0) //Insert if both are files
            {
                Items.Insert(i, entry);
                inserted = true;
                break;
            }
        }
            
        if (!inserted) Items.Add(entry);
        entry.TopFolder = this;
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
            for (int i = 0; i < folder.Items.Count; i++)
            {
                folder.Remove(folder.Items[i]);
                i--;
            }
        }

        (entry.Root as ProjectRoot)?.UnregisterEntry(entry);
        Items.Remove(entry);

        //Collapse folder if empty
        if (Items.Count == 0) IsExpanded = false;
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
                Tools.CopyFile(path, destination, overwrite);

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
            
        if (!createNew && Search(Path.Combine(RelativePath, path), false) is ProjectFile file)
        {
            return file;
        }
            
        var fullPath = Path.Combine(FullPath, path);

        if (createNew)
        {
            fullPath = fullPath.CheckNameFile();
            path = Path.GetFileName(fullPath);
        }
            
        var projFile = new ProjectFile(path, this); 
            
        if (!File.Exists(fullPath))
        {
            if (createNew)
            {
                try
                {
                    Tools.CreateFile(fullPath);
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
        
    public IProjectFolder AddFolder(string path, bool createNew = false)
    {
        var folderParts = path.Split(Path.DirectorySeparatorChar);
        if (folderParts.Length > 1)
        {
            if (Search(folderParts[0]) is ProjectFolder existing)
                return existing.AddFolder(path.Remove(0, folderParts[0].Length + 1), createNew);

            var created = AddFolder(folderParts[0], createNew);
            return created.AddFolder(path.Remove(0, folderParts[0].Length + 1), createNew);
        }

        if (!createNew && Search(path, false) is ProjectFolder existingFolder)
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

        var pf = new ProjectFolder(path, this);
        
        Insert(pf);
        return pf;
    }
    
    public IProjectEntry? Search(string path, bool recursive = true) //TODO Optimize
    {
        foreach (var i in Items)
        {
            if (path.Equals(i.Header, StringComparison.OrdinalIgnoreCase) //Search for name equality
                || path.EqualPaths(i.RelativePath)
                || path.EqualPaths(i.FullPath))
                return i;
            if (recursive && i is ProjectFolder folder)
            {
                var pe = folder.Search(path);
                if (pe != null) return pe;
            }
        }

        return null;
    }
}