using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectSystem.Models;

public class ProjectFolder : ProjectEntry, IProjectFolder
{
    protected ProjectFolder(string header, IProjectFolder? topFolder, bool defaultFolderAnimation = true) : base(header,
        topFolder)
    {
        IDisposable? iconDisposable = null;

        this.WhenValueChanged(x => x.IsExpanded).Subscribe(x =>
        {
            iconDisposable?.Dispose();

            if (x)
            {
                LoadContent();

                if (defaultFolderAnimation)
                    iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.FolderOpen16X").Subscribe(y =>
                    {
                        Icon = y as IImage;
                    }).DisposeWith(Disposables);
            }
            else
            {
                foreach (var subEntity in Entities.ToArray())
                    Remove(subEntity);

                if (Directory.Exists(FullPath) &&
                    Directory.EnumerateFileSystemEntries(FullPath).Any())
                    Children.Add(new LoadingDummyNode());

                if (defaultFolderAnimation)
                    iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.Folder16X")
                        .Subscribe(y => { Icon = y as IImage; }).DisposeWith(Disposables);
            }
        }).DisposeWith(Disposables);
    }

    public void Add(IProjectEntry entry)
    {
        entry.TopFolder = this;
        Insert(entry);
    }

    public void Remove(IProjectEntry entry)
    {
        if (entry is ProjectFolder folder)
            for (var i = 0; i < folder.Entities.Count; i++)
            {
                folder.Remove(folder.Entities[i]);
                i--;
            }
        
        Children.Remove(entry);
        Entities.Remove(entry);
        
        entry.Dispose();

        //Collapse folder if empty
        if (Children.Count == 0) IsExpanded = false;
    }

    public void SetIsExpanded(bool newValue)
    {
        IsExpanded = newValue;
    }

    public IProjectFile AddFile(string relativePath, bool createNew = false)
    {
        var split = relativePath.LastIndexOf(Path.DirectorySeparatorChar);

        if (split > 0)
        {
            var folderPath = relativePath[..split];
            var pf = AddFolder(folderPath);
            return pf.AddFile(relativePath.Substring(split + 1, relativePath.Length - split - 1), createNew);
        }

        if (!createNew && SearchEntries(relativePath) is ProjectFile file) return file;

        var fullPath = Path.Combine(FullPath, relativePath);

        if (createNew)
        {
            fullPath = fullPath.CheckNameFile();
            relativePath = Path.GetFileName(fullPath);
        }

        var projFile = ConstructNewProjectFile(relativePath, this);

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

    public IProjectFolder AddFolder(string relativePath, bool createNew = false)
    {
        var split = relativePath.IndexOf(Path.DirectorySeparatorChar);
        if (split >= 1)
        {
            var folderName = relativePath[..split];
            if (SearchEntries(folderName) is ProjectFolder existing)
                return existing.AddFolder(relativePath.Remove(0, folderName.Length + 1), createNew);

            var created = AddFolder(folderName, createNew);
            return created.AddFolder(relativePath.Remove(0, folderName.Length + 1), createNew);
        }

        if (!createNew && SearchEntries(relativePath) is ProjectFolder existingFolder) return existingFolder;

        var fullPath = Path.Combine(FullPath, relativePath);
        if (createNew)
        {
            fullPath = fullPath.CheckNameDirectory();
            relativePath = Path.GetFileName(fullPath);
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
        
        var pf = ConstructNewProjectFolder(relativePath, this);

        Insert(pf);
        return pf;
    }

    public IProjectEntry? GetLoadedEntry(string relativePath)
    {
        var split = relativePath.IndexOf(Path.DirectorySeparatorChar);

        if (split > -1)
        {
            var subFolder = SearchEntries(relativePath[..split]) as IProjectFolder;
            return subFolder?.GetLoadedEntry(relativePath.Remove(0, split + 1));
        }

        return SearchEntries(relativePath);
    }

    private IProjectEntry? SearchEntries(string relativePath)
    {
        if (relativePath is "" or ".") return this;

        foreach (var i in Entities)
            if (relativePath.Equals(i.Name, StringComparison.OrdinalIgnoreCase))
                return i;

        return null;
    }

    private void Insert(IProjectEntry entry)
    {
        //Insert in correct posiion
        var inserted = false;
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is IProjectEntry && ((entry is ProjectFolder && Children[i] is not ProjectFolder) ||
                                                 (entry is ProjectFolder && Children[i] is ProjectFolder &&
                                                  string.Compare(entry.Header, Children[i].Header,
                                                      StringComparison.OrdinalIgnoreCase) <=
                                                  0) || //Insert if both are folders
                                                 (entry is not ProjectFolder && Children[i] is not ProjectFolder &&
                                                  string.Compare(entry.Header, Children[i].Header,
                                                      StringComparison.OrdinalIgnoreCase) <=
                                                  0))) //Insert if both are files
            {
                Children.Insert(i, entry);
                inserted = true;
                break;
            }
        }
        
        if (!inserted) Children.Add(entry);
        entry.TopFolder = this;

        Entities.Add(entry);
    }

    protected virtual IProjectFolder ConstructNewProjectFolder(string path, IProjectFolder topFolder)
    {
        return new ProjectFolder(path, topFolder);
    }

    protected virtual IProjectFile ConstructNewProjectFile(string path, IProjectFolder topFolder)
    {
        return new ProjectFile(path, topFolder);
    }

    public IProjectEntry? GetEntry(string? relativePath)
    {
        if(relativePath == null) return null;
        
        var visual = SearchEntries(relativePath);
        if (visual != null) return visual;
        
        // Create it in the visual tree and return it.
        var fullPath = Path.Combine(FullPath, relativePath);
        if (!File.Exists(fullPath)) return null;
        
        var fileInfo = new FileInfo(fullPath);

        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
        {
            return AddFolder(relativePath);
        }
        else
        {
            return AddFile(relativePath);
        }
    }

    public virtual IProjectFile? GetFile(string? relativePath)
    {
        return GetEntry(relativePath) as IProjectFile;
    }

    public virtual IProjectFile? GetFolder(string? relativePath)
    {
        return GetEntry(relativePath) as IProjectFile;
    }

    public virtual IEnumerable<string> GetFiles(string searchPattern = "*")
    {
        return Directory.EnumerateFiles(FullPath, searchPattern, SearchOption.AllDirectories);
    }
    
    public virtual void LoadContent()
    {
        Children.Clear();
        Entities.Clear();

        if (!Directory.Exists(FullPath))
        {
            LoadingFailed = true;
            return;
        }

        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        var directoryMatches = Root.GetFiles();

        foreach (var match in directoryMatches)
        {
            var newFolder = new ProjectFolder(Path.GetFileName(match), this);
            Children.Add(newFolder);
            Entities.Add(newFolder);
        }

        var fileMatches = Directory.EnumerateFiles(FullPath, "*.*", options);

        foreach (var match in fileMatches)
        {
            var newFile = new ProjectFile(Path.GetFileName(match), this);
            Children.Add(newFile);
            Entities.Add(newFile);
        }
    }
}