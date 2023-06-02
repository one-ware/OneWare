using DynamicData.Binding;
using Prism.Ioc;

using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.ProjectExplorer.Models;

public class ProjectFolder : ProjectEntry, IProjectFolder
{
    public ProjectFolder(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
        this.WhenValueChanged(x => x.IsExpanded).Subscribe(x =>
        {
            
        });
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
            fullPath = ProjectExplorerHelpers.CheckNameFile(fullPath);
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
            fullPath = ProjectExplorerHelpers.CheckNameDirectory(fullPath);
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
                || path.IsSamePathAs(i.RelativePath)
                || path.IsSamePathAs(i.FullPath))
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