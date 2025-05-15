﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DynamicData.Binding;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectSystem.Models;

public class ProjectFolder : ProjectEntry, IProjectFolder
{
    private readonly ILogger _logger;

    // Constructor now accepts ILogger via dependency injection
    public ProjectFolder(string header, IProjectFolder? topFolder, bool defaultFolderAnimation = true, ILogger logger)
        : base(header, topFolder)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (defaultFolderAnimation)
        {
            IDisposable? iconDisposable = null;
            this.WhenValueChanged(x => x.IsExpanded).Subscribe(x =>
            {
                iconDisposable?.Dispose();
                if (!x)
                    iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.Folder16X").Subscribe(y =>
                    {
                        Icon = y as IImage;
                    });
                else
                    iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.FolderOpen16X")
                        .Subscribe(y => { Icon = y as IImage; });
            });
        }
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

        (entry.Root as ProjectRoot)?.UnregisterEntry(entry);
        Children.Remove(entry);
        Entities.Remove(entry);

        //Collapse folder if empty
        if (Children.Count == 0) IsExpanded = false;
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

        if (!createNew && SearchName(path) is ProjectFile file) return file;

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
                try
                {
                    PlatformHelper.CreateFile(fullPath);
                }
                catch (Exception e)
                {
                    _logger?.Error(e.Message, e);
                }
            else
                projFile.LoadingFailed = true;
        }

        Insert(projFile);
        return projFile;
    }

    public IProjectFolder AddFolder(string path, bool createNew = false)
    {
        var split = path.IndexOf(Path.DirectorySeparatorChar);
        if (split >= 1)
        {
            var folderName = path[..split];
            if (SearchName(folderName) is ProjectFolder existing)
                return existing.AddFolder(path.Remove(0, folderName.Length + 1), createNew);

            var created = AddFolder(folderName, createNew);
            return created.AddFolder(path.Remove(0, folderName.Length + 1), createNew);
        }

        if (!createNew && SearchName(path) is ProjectFolder existingFolder) return existingFolder;

        var fullPath = Path.Combine(FullPath, path);
        if (createNew)
        {
            fullPath = fullPath.CheckNameDirectory();
            path = Path.GetFileName(fullPath);
        }

        if (!Directory.Exists(fullPath))
            try
            {
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception e)
            {
                _logger?.Error(e.Message, e);
            }

        var pf = ConstructNewProjectFolder(path, this);

        Insert(pf);
        return pf;
    }

    public IProjectEntry? SearchName(string path)
    {
        if (path is "" or ".") return this;

        foreach (var i in Entities)
            if (path.Equals(i.Name, StringComparison.OrdinalIgnoreCase))
                return i;

        return null;
    }

    public IProjectEntry? SearchRelativePath(string path)
    {
        var split = path.IndexOf(Path.DirectorySeparatorChar);

        if (split > -1)
        {
            var subFolder = SearchName(path[..split]) as IProjectFolder;
            return subFolder?.SearchRelativePath(path.Remove(0, split + 1));
        }

        return SearchName(path);
    }

    public IProjectEntry? SearchFullPath(string path)
    {
        var relativePath = Path.GetRelativePath(FullPath, path);
        return SearchRelativePath(relativePath);
    }

    private void Insert(IProjectEntry entry)
    {
        // Insert in correct position
        var inserted = false;
        for (var i = 0; i < Children.Count; i++)
            if (Children[i] is IProjectEntry && ((entry is ProjectFolder && Children[i] is not ProjectFolder) ||
                                                 (entry is ProjectFolder && Children[i] is ProjectFolder &&
                                                  string.CompareOrdinal(entry.Header, Children[i].Header) <=
                                                  0) || // Insert if both are folders
                                                 (entry is not ProjectFolder && Children[i] is not ProjectFolder &&
                                                  string.CompareOrdinal(entry.Header, Children[i].Header) <=
                                                  0))) // Insert if both are files
            {
                Children.Insert(i, entry);
                inserted = true;
                break;
            }

        if (!inserted) Children.Add(entry);
        entry.TopFolder = this;

        Entities.Add(entry);
        (entry.Root as ProjectRoot)?.RegisterEntry(entry);
    }

    public IProjectFile? ImportFile(string path, bool overwrite)
    {
        var destination = Path.Combine(FullPath, Path.GetFileName(path));

        // Check if File exists
        if (!File.Exists(path))
        {
            _logger?.Warning($"Cannot import {path}. File does not exist");
            return null;
        }

        try
        {
            if (!path.EqualPaths(destination))
                PlatformHelper.CopyFile(path, destination, overwrite);

            return AddFile(Path.GetFileName(destination));
        }
        catch (Exception e)
        {
            _logger?.Error(e.Message, e);
            return null;
        }
    }

    protected virtual IProjectFolder ConstructNewProjectFolder(string path, IProjectFolder topFolder)
    {
        return new ProjectFolder(path, topFolder, logger: _logger);
    }

    protected virtual IProjectFile ConstructNewProjectFile(string path, IProjectFolder topFolder)
    {
        return new ProjectFile(path, topFolder);
    }
}
