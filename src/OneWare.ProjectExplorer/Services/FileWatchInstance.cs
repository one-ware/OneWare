using Avalonia.Threading;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchInstance : IDisposable
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IDockService _dockService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly IProjectRoot _root;
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly object _lock = new();
    private DispatcherTimer? _timer;
    private readonly Dictionary<string, List<FileSystemEventArgs>> _changes = new();

    public FileWatchInstance(IProjectRoot root, IProjectExplorerService projectExplorerService, IDockService dockService, ISettingsService settingsService, ILogger logger)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _settingsService = settingsService;
        _logger = logger;

        _fileSystemWatcher = new FileSystemWatcher(root.FullPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true
        };

        _fileSystemWatcher.Changed += File_Changed;
        _fileSystemWatcher.Deleted += File_Changed;
        _fileSystemWatcher.Renamed += File_Changed;
        _fileSystemWatcher.Created += File_Changed;

        try
        {
            _settingsService.GetSettingObservable<bool>("Editor_DetectExternalChanges").Subscribe(x =>
            {
                _fileSystemWatcher.EnableRaisingEvents = x;

                _timer?.Stop();
                if (x)
                {
                    _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) =>
                    {
                        lock (_lock)
                        {
                            ProcessChanges();
                        }
                    });
                    _timer.Start();
                }
            });

        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private void File_Changed(object source, FileSystemEventArgs e)
    {
        if (e.Name == null) return;
        lock (_lock)
        {
            _changes.TryAdd(e.FullPath, new List<FileSystemEventArgs>());
            _changes[e.FullPath].Add(e);
        }
    }

    private void ProcessChanges()
    {
        foreach(var change in _changes)
        {
            _ = ProcessAsync(change.Key, change.Value);
        }

        //Task.WhenAll(_changes.Select(x => ProcessAsync(x.Key, x.Value)));
        _changes.Clear();
    }

    private async Task ProcessAsync(string path, List<FileSystemEventArgs> changes)
    {
        var entry = _root.Search(path);

        var lastArg = changes.Last();
        
        if (entry is not null)
        {
            switch (lastArg.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Renamed:
                case WatcherChangeTypes.Changed:
                    if (entry is IFile file)
                    {
                        if(File.GetLastWriteTime(file.FullPath) > file.LastSaveTime)
                            await _projectExplorerService.ReloadAsync(entry);
                    }
                    else await _projectExplorerService.ReloadAsync(entry);
                    return;
                case WatcherChangeTypes.Deleted:
                    await _projectExplorerService.DeleteAsync(entry);
                    return;
            }
        }

        if (entry is null)
        {
            var relativePath = Path.GetRelativePath(_root.ProjectPath, path);
            
            switch (lastArg.ChangeType)
            {
                case WatcherChangeTypes.Renamed:
                    if (lastArg is RenamedEventArgs {Name: not null, OldFullPath: not null} renamedEventArgs && _root.Search(renamedEventArgs.OldFullPath) is {} oldEntry)
                    {
                        if (oldEntry is IProjectFile file)
                        {
                            _dockService.OpenFiles.TryGetValue(file, out var tab);
                            await _projectExplorerService.RemoveAsync(oldEntry);
                            var newItem = _root.AddFile(relativePath);
                            if (tab is IEditor editor) editor.FullPath = newItem.FullPath;
                            tab?.InitializeContent();
                        }
                        else
                        {
                            await _projectExplorerService.RemoveAsync(oldEntry);
                            var folder = _root.AddFolder(relativePath);
                            _projectExplorerService.ImportFolderRecursive(path, folder);
                        }
                    }
                    return;
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed when changes.Any(x => x.ChangeType is WatcherChangeTypes.Created):
                    var attr = File.GetAttributes(path);
                    
                    if (attr.HasFlag(FileAttributes.Directory))
                        _root.AddFolder(relativePath);
                    else
                        _root.AddFile(relativePath);
                    return;
            }
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _fileSystemWatcher.Dispose();
    }
}