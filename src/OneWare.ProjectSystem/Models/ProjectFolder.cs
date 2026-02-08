using System.Collections.ObjectModel;
using System.Threading;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectSystem.Models;

public class ProjectFolder : ProjectEntry, IProjectFolder
{
    private CancellationTokenSource? _loadCancellation;
    private bool _isLoaded;
    private bool _isLoading;

    protected ProjectFolder(string header, IProjectFolder? topFolder) : base(header,
        topFolder)
    {
        this.WhenValueChanged(x => x.IsExpanded).Subscribe(isExpanded =>
        {
            if (isExpanded)
            {
                StartLoad();
            }
            else
            {
                CancelLoad();
                _isLoaded = false;

                if (Children != null)
                    foreach (var subEntity in Children.OfType<IProjectEntry>().ToArray())
                        Remove(subEntity);

                if (Directory.Exists(FullPath) &&
                    Directory.EnumerateFileSystemEntries(FullPath).Any())
                {
                    if (Children == null) Children = new ObservableCollection<IProjectExplorerNode>();
                    Children.Add(new LoadingDummyNode());
                }
            }
        }).DisposeWith(Disposables);
    }

    public override IconModel? IconModel { get; } = null;

    public void Add(IProjectEntry entry)
    {
        entry.TopFolder = this;
        Insert(entry);
    }

    public void Remove(IProjectEntry entry)
    {
        if (entry is ProjectFolder {Children: not null} folder)
        {
            for (var i = 0; i < folder.Children.Count; i++)
            {
                if (folder.Children[i] is IProjectEntry subEntry)
                {
                    folder.Remove(subEntry);
                    i--;
                }
            }
        }

        Children?.Remove(entry);

        entry.Dispose();

        //Collapse folder if empty
        if (Children?.Count == 0) IsExpanded = false;
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

    public virtual IProjectEntry? GetLoadedEntry(string relativePath)
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

        if(Children == null) return null;
        
        foreach (var i in Children.OfType<IProjectEntry>())
            if (relativePath.Equals(i.Name, StringComparison.OrdinalIgnoreCase))
                return i;

        return null;
    }

    private void Insert(IProjectEntry entry)
    {
        if (Children == null) Children = new ObservableCollection<IProjectExplorerNode>();
        
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
        if (relativePath == null) return null;

        if (!Root.IsPathIncluded(Path.Combine(RelativePath, relativePath)))
            return null;

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

    public virtual IEnumerable<string> GetFiles(string searchPattern = "*", bool recursive = true)
    {
        var path = FullPath;

        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
            RecurseSubdirectories = recursive,
        };

        foreach (var file in Directory.EnumerateFiles(path, searchPattern, options))
        {
            var relativeToRoot = Path.GetRelativePath(path, file);
            if (Root.IsPathIncluded(relativeToRoot)) yield return Path.GetRelativePath(FullPath, file);
        }
    }

    public IEnumerable<string> GetDirectories(string searchPattern = "*", bool recursive = true)
    {
        var path = FullPath;

        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
            RecurseSubdirectories = recursive,
        };

        foreach (var file in Directory.EnumerateDirectories(path, searchPattern, options))
        {
            var relativeToRoot = Path.GetRelativePath(path, file);
            if (Root.IsPathIncluded(relativeToRoot)) yield return Path.GetRelativePath(FullPath, file);
        }
    }

    protected virtual async Task LoadContentAsync(CancellationToken cancellationToken, bool forceReload = false)
    {
        if (_isLoading) return;
        if (_isLoaded && !forceReload) return;

        _isLoading = true;
        Children?.Clear();

        if (!Directory.Exists(FullPath))
        {
            LoadingFailed = true;
            _isLoading = false;
            return;
        }

        LoadingFailed = false;

        // Batch size (tweakable)
        const int batchSize = 50;

        if (Children == null) Children = new ObservableCollection<IProjectExplorerNode>();

        // Load folders first
        await foreach (var folder in EnumerateFoldersAsync(batchSize, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;
            Children.Add(folder);
        }

        // Load files after
        await foreach (var file in EnumerateFilesAsync(batchSize, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;
            Children.Add(file);
        }

        if (!cancellationToken.IsCancellationRequested)
            _isLoaded = true;

        _isLoading = false;
    }

    private async IAsyncEnumerable<ProjectFolder> EnumerateFoldersAsync(int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var matches = GetDirectories("*", false);

        int count = 0;

        foreach (var match in matches)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            yield return new ProjectFolder(Path.GetFileName(match), this);

            count++;

            // Yield control back to UI every batch
            if (count % batchSize == 0)
            {
                await Task.Yield();
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    private async IAsyncEnumerable<ProjectFile> EnumerateFilesAsync(int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var matches = GetFiles("*", false);

        int count = 0;

        foreach (var match in matches)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            yield return new ProjectFile(Path.GetFileName(match), this);

            count++;

            if (count % batchSize == 0)
            {
                await Task.Yield();
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    private void StartLoad(bool forceReload = false)
    {
        CancelLoad();
        _loadCancellation = new CancellationTokenSource();
        _ = LoadContentAsync(_loadCancellation.Token, forceReload);
    }

    private void CancelLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _isLoaded = false;
    }
}
