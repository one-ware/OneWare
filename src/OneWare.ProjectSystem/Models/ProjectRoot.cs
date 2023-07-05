using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OneWare.Shared;
using OneWare.Shared.Converters;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectRoot : ProjectFolder, IProjectRoot
{
    public string RootFolderPath { get; init; }

    public List<IProjectFile> Files { get; } = new();

    public override string FullPath => RootFolderPath;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            SetProperty(ref _isActive, value);
            FontWeight = value ? FontWeight.Bold : FontWeight.Regular;
        }
    }

    protected ProjectRoot(string rootFolderPath) : base(Path.GetFileName(rootFolderPath), null)
    {
        RootFolderPath = rootFolderPath;
        TopFolder = this;

        var observable = Application.Current?.GetResourceObservable("Namespace");
        observable?.Subscribe(x =>
        {
            Icon = x as IImage;
        });
    }
    
    public void Cleanup()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher?.Dispose();
            _fileWatcher = null;
        }
    }

    internal void RegisterEntry(IProjectEntry entry)
    {
        if(entry is ProjectFile file) Files.Add(file);
    }
    
    internal void UnregisterEntry(IProjectEntry entry)
    {
        if (entry is ProjectFile file) Files.Remove(file);
    }
    
    #region FileSystemWatcher

    private FileSystemWatcher? _fileWatcher;

    public void SetupFileWatcher()
    {
        if (_fileWatcher != null) _fileWatcher.EnableRaisingEvents = false;
        if (Directory.Exists(FullPath))
        {
            _fileWatcher = new FileSystemWatcher(FullPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true
            };
            _fileWatcher.Changed += OnExtFileChanged;
            _fileWatcher.Deleted += OnExtFileDeleted;
            _fileWatcher.Renamed += OnExtFileRenamed;
            _fileWatcher.Created += OnExtFileCreated;

            try
            {
                _fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(RelativePath + ": " + e.Message, e);
            }
        }
    }

    private readonly List<string> _watcherHandle = new();

    private void OnExtFileChanged(object source, FileSystemEventArgs e)
    {
        if (!e.FullPath.Contains(".git")) return;
        if (_fileWatcher == null || e.Name == null) return;
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_watcherHandle.Contains(e.Name)) return;
            _watcherHandle.Add(e.Name);
            await ContainerLocator.Container.Resolve<IProjectExplorerService>(e.FullPath).HandleFileChangeAsync(e.FullPath);
            _watcherHandle.Remove(e.Name);
        }, DispatcherPriority.Background);
    }

    private void OnExtFileDeleted(object source, FileSystemEventArgs e)
    {
        if (_fileWatcher == null || e.Name == null) return;
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_watcherHandle.Contains(e.Name)) return;
            _watcherHandle.Add(e.Name);
            if (Search(e.Name) is { } entry)
            {
                entry.LoadingFailed = true;
            }

            _watcherHandle.Remove(e.Name);
        }, DispatcherPriority.Background);
    }

    private void OnExtFileCreated(object source, FileSystemEventArgs e)
    {
        if (_fileWatcher == null || e.Name == null) return;
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_watcherHandle.Contains(e.Name)) return;
            _watcherHandle.Add(e.Name);
            if (Search(e.Name) is { } entry && entry.LoadingFailed)
                entry.LoadingFailed = false;
            _watcherHandle.Remove(e.Name);
        }, DispatcherPriority.Background);
    }

    private void OnExtFileRenamed(object source, RenamedEventArgs e)
    {
        if (_fileWatcher == null || e.Name == null) return;
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (e.OldName != null && Search(e.OldName) is { } entry)
            {
                //entry.Header = e.Name;
                if (_watcherHandle.Contains(e.Name)) return;

                if (entry.LoadingFailed)
                {
                    _watcherHandle.Add(e.Name);
                    await ContainerLocator.Container.Resolve<IProjectExplorerService>(e.FullPath).HandleFileChangeAsync(e.FullPath);
                    _watcherHandle.Remove(e.Name);
                }
            }
        }, DispatcherPriority.Background);
    }

    #endregion
}