using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectExplorer.Services;

public class ProjectWatchInstance : IDisposable
{
    private readonly Dictionary<string, List<FileSystemEventArgs>> _changes = new();
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly Lock _lock = new();
    private readonly IMainDockService _mainDockService;
    private readonly FileSystemWatcher _parentWatcher;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IProjectRoot _root;
    private DispatcherTimer? _timer;

    public ProjectWatchInstance(IProjectRoot root, IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService, ISettingsService settingsService, ILogger logger)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;

        _fileSystemWatcher = new FileSystemWatcher(root.FullPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true
        };

        _fileSystemWatcher.Changed += File_Changed;
        _fileSystemWatcher.Deleted += File_Changed;
        _fileSystemWatcher.Renamed += File_Changed;
        _fileSystemWatcher.Created += File_Changed;

        _fileSystemWatcher.Error += (sender, args) => { Console.WriteLine("Error"); };

        var parent = Directory.GetParent(root.FullPath)!;

        _parentWatcher = new FileSystemWatcher(parent.FullName)
        {
            NotifyFilter = NotifyFilters.DirectoryName
        };

        _parentWatcher.Renamed += (s, e) =>
        {
            if (string.Equals(e.OldFullPath, root.FullPath, StringComparison.OrdinalIgnoreCase))
                // root was renamed, we detect is as it was deleted
                Dispatcher.UIThread.Post(() =>
                {
                    root.LoadingFailed = true;
                    root.IsExpanded = false;
                });
        };

        _parentWatcher.Deleted += (s, e) =>
        {
            if (string.Equals(e.FullPath, root.FullPath, StringComparison.OrdinalIgnoreCase))
                // root was deleted
                Dispatcher.UIThread.Post(() =>
                {
                    root.LoadingFailed = true;
                    root.IsExpanded = false;
                });
        };

        try
        {
            settingsService.GetSettingObservable<bool>("Editor_DetectExternalChanges").Subscribe(x =>
            {
                _fileSystemWatcher.EnableRaisingEvents = x;
                _parentWatcher.EnableRaisingEvents = x;

                _timer?.Stop();
                if (!x) return;
                _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) =>
                {
                    lock (_lock)
                    {
                        ProcessChanges();
                    }
                });
                _timer.Start();
            });
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _fileSystemWatcher.Dispose();
        _parentWatcher.Dispose();
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
        foreach (var change in _changes) _ = ProcessAsync(change.Key, change.Value);
        
        _changes.Clear();
    }

    private async Task ProcessAsync(string path, IReadOnlyCollection<FileSystemEventArgs> changes)
    {
        try
        {
            var attributes = FileAttributes.None;

            if (File.Exists(path) || Directory.Exists(path)) attributes = File.GetAttributes(path);

            var lastArg = changes.Last();

            var openTab = _mainDockService.OpenFiles
                .FirstOrDefault(x => x.Key.EqualPaths(path));

            if (openTab.Value != null)
            {
                switch (lastArg.ChangeType)
                {
                    case WatcherChangeTypes.Changed:
                    {
                        var lastWriteTime = File.GetLastWriteTime(path);

                        if (openTab.Value.LastSaveTime > lastWriteTime)
                        {
                            openTab.Value.InitializeContent();
                        }
                        break;
                    }
                }
            }
            
            var relativePath = Path.GetRelativePath(_root.RootFolderPath, path);
            var entry = _root.GetLoadedEntry(relativePath);

            if (entry is not null)
                switch (lastArg.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Renamed:
                    case WatcherChangeTypes.Changed:
                        if (lastArg is RenamedEventArgs rea && !File.Exists(rea.OldFullPath) &&
                            _root.GetLoadedEntry(Path.GetRelativePath(_root.RootFolderPath, rea.OldFullPath)) is { } deleted)
                        {
                            deleted.TopFolder?.Remove(deleted);
                        }
                        else if (entry is IProjectRootWithFile project)
                        {
                            var lastWriteTime = File.GetLastWriteTime(project.FullPath);
                            if (lastWriteTime > project.LastSaveTime)
                                await _projectExplorerService.ReloadProjectAsync(project);
                        }

                        return;
                    case WatcherChangeTypes.Deleted:
                        entry.TopFolder?.Remove(entry);
                        return;
                }

            if (entry is null)
                switch (lastArg.ChangeType)
                {
                    case WatcherChangeTypes.Renamed:
                        if (lastArg is RenamedEventArgs { Name: not null } renamedEventArgs &&
                            _root.GetLoadedEntry(Path.GetRelativePath(_root.RootFolderPath,
                                renamedEventArgs.OldFullPath)) is { } oldEntry)
                        {
                            oldEntry.TopFolder?.Remove(oldEntry);
                            _root.OnExternalEntryAdded(path, attributes);
                        }

                        return;
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Changed when changes.Any(x => x.ChangeType is WatcherChangeTypes.Created):
                        _root.OnExternalEntryAdded(path, attributes);
                        return;
                    case WatcherChangeTypes.Deleted:
                        if (_root.ProjectPath.EqualPaths(path)) _root.LoadingFailed = true;

                        return;
                }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e, false);
        }
    }
}