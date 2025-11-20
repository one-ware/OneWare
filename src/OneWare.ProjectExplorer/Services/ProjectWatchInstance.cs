using Avalonia.Threading;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Services;

public class ProjectWatchInstance : IDisposable
{
    private readonly Dictionary<string, List<FileSystemEventArgs>> _changes = new();
    private readonly IDockService _dockService;
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly Lock _lock = new();
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IProjectRoot _root;
    private readonly IWindowService _windowService;
    private DispatcherTimer? _timer;

    public ProjectWatchInstance(IProjectRoot root, IProjectExplorerService projectExplorerService,
        IDockService dockService, ISettingsService settingsService, IWindowService windowService, ILogger logger)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _windowService = windowService;

        _fileSystemWatcher = new FileSystemWatcher(root.FullPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true
        };

        _fileSystemWatcher.Changed += File_Changed;
        _fileSystemWatcher.Deleted += File_Changed;
        _fileSystemWatcher.Renamed += File_Changed;
        _fileSystemWatcher.Created += File_Changed;

        _fileSystemWatcher.Error += (sender, args) =>
        {
            Console.WriteLine("Error");
        };

        try
        {
            settingsService.GetSettingObservable<bool>("Editor_DetectExternalChanges").Subscribe(x =>
            {
                _fileSystemWatcher.EnableRaisingEvents = x;

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

        //Task.WhenAll(_changes.Select(x => ProcessAsync(x.Key, x.Value)));
        _changes.Clear();
    }

    private async Task ProcessAsync(string path, IReadOnlyCollection<FileSystemEventArgs> changes)
    {
        try
        {
            var attributes = FileAttributes.None;

            if (File.Exists(path) || Directory.Exists(path)) attributes = File.GetAttributes(path);

            var entry = _root.SearchFullPath(path);

            var lastArg = changes.Last();

            if (entry is not null)
                switch (lastArg.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Renamed:
                    case WatcherChangeTypes.Changed:
                        if (lastArg is RenamedEventArgs rea && !File.Exists(rea.OldFullPath) &&
                            _root.SearchFullPath(rea.OldFullPath) is { } deleted)
                            await _projectExplorerService.RemoveAsync(deleted);
                        if (entry is ISavable savable)
                        {
                            var lastWriteTime = File.GetLastWriteTime(savable.FullPath);
                            if (lastWriteTime > savable.LastSaveTime)
                                await _projectExplorerService.ReloadAsync(entry);

                            if (savable is IProjectFile { Root: IProjectRootWithFile rootWithFile } &&
                                rootWithFile.ProjectFilePath == savable.FullPath &&
                                lastWriteTime > rootWithFile.LastSaveTime)
                                await _projectExplorerService.ReloadAsync(rootWithFile);
                        }
                        else
                        {
                            await _projectExplorerService.ReloadAsync(entry);
                        }

                        return;
                    case WatcherChangeTypes.Deleted:
                        await _projectExplorerService.RemoveAsync(entry);
                        return;
                }

            if (entry is null)
                switch (lastArg.ChangeType)
                {
                    case WatcherChangeTypes.Renamed:
                        if (lastArg is RenamedEventArgs { Name: not null, OldFullPath: not null } renamedEventArgs &&
                            _root.SearchFullPath(renamedEventArgs.OldFullPath) is { } oldEntry)
                        {
                            if (oldEntry is IProjectFile file)
                            {
                                _dockService.OpenFiles.TryGetValue(file, out var tab);
                                _dockService.OpenFiles.Remove(file);
                                
                                await _projectExplorerService.RemoveAsync(oldEntry);
                                _root.OnExternalEntryAdded(path, attributes);

                                if (tab is not null)
                                {
                                    tab.FullPath = path;
                                    tab.InitializeContent();
                                    
                                    if(tab.CurrentFile != null) 
                                        _dockService.OpenFiles.TryAdd(tab.CurrentFile!, tab);
                                }
                            }
                            else
                            {
                                await _projectExplorerService.RemoveAsync(oldEntry);
                                _root.OnExternalEntryAdded(path, attributes);
                            }
                        }

                        return;
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Changed when changes.Any(x => x.ChangeType is WatcherChangeTypes.Created):
                        _root.OnExternalEntryAdded(path, attributes);
                        var openTab = _dockService.OpenFiles.FirstOrDefault(x => x.Key.FullPath.EqualPaths(path));
                        if (openTab.Key is not null)
                            openTab.Value.InitializeContent();
                        return;
                    case WatcherChangeTypes.Changed:
                        if (_root is ISavable savable && _root.ProjectPath.EqualPaths(path))
                            if (File.GetLastWriteTime(_root.FullPath) > savable.LastSaveTime)
                                await _projectExplorerService.ReloadAsync(_root);
                        return;
                    case WatcherChangeTypes.Deleted:
                        if (_root.ProjectPath.EqualPaths(path))
                        {
                            _root.LoadingFailed = true;
                        }
                        return;
                }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e, false);
        }
    }
}