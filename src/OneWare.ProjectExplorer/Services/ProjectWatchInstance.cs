using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectExplorer.Services;

public class ProjectWatchInstance : IDisposable
{
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly Lock _gate = new();
    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly FileSystemWatcher _parentWatcher;
    private readonly List<FileSystemEventArgs> _pending = new();
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IProjectRoot _root;
    private DispatcherTimer? _timer;

    public ProjectWatchInstance(IProjectRoot root, IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService, ISettingsService settingsService, ILogger logger)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _logger = logger;

        _fileSystemWatcher = new FileSystemWatcher(root.FullPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
        };

        _fileSystemWatcher.Changed += File_Changed;
        _fileSystemWatcher.Deleted += File_Changed;
        _fileSystemWatcher.Renamed += File_Changed;
        _fileSystemWatcher.Created += File_Changed;

        _fileSystemWatcher.Error += (_, _) => _ = Task.Run(FullResyncAsync);

        var parent = Directory.GetParent(root.FullPath)!;

        _parentWatcher = new FileSystemWatcher(parent.FullName)
        {
            NotifyFilter = NotifyFilters.DirectoryName
        };

        _parentWatcher.Renamed += (_, e) =>
        {
            if (string.Equals(e.OldFullPath, root.FullPath, StringComparison.OrdinalIgnoreCase))
                // root was renamed, we detect is as it was deleted
                Dispatcher.UIThread.Post(() =>
                {
                    root.LoadingFailed = true;
                    root.IsExpanded = false;
                });
        };

        _parentWatcher.Deleted += (_, e) =>
        {
            if (string.Equals(e.FullPath, root.FullPath, StringComparison.OrdinalIgnoreCase))
                // root was deleted
                Dispatcher.UIThread.Post(() =>
                {
                    root.LoadingFailed = true;
                    root.IsExpanded = false;
                });
        };
        _parentWatcher.Error += (_, _) => _ = Task.Run(FullResyncAsync);

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
                    ProcessChanges();
                });
                _timer.Start();
            });
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
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
        if (string.IsNullOrWhiteSpace(e.FullPath)) return;
        if (IsHiddenPath(e.FullPath)) return;
        lock (_gate)
        {
            _pending.Add(e);
        }
    }

    private void ProcessChanges()
    {
        List<FileSystemEventArgs> batch;
        lock (_gate)
        {
            if (_pending.Count == 0) return;
            batch = new List<FileSystemEventArgs>(_pending);
            _pending.Clear();
        }

        _ = Task.Run(async () =>
        {
            var plan = BuildPlan(batch);
            await ApplyPlanAsync(plan);
        });
    }

    private FsPlan BuildPlan(IEnumerable<FileSystemEventArgs> events)
    {
        var removeTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removeEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var upsertTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var upsertEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var touchEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reconcileParents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in events.OfType<RenamedEventArgs>())
        {
            var oldPath = e.OldFullPath;
            var newPath = e.FullPath;

            if (!string.IsNullOrWhiteSpace(oldPath))
            {
                MarkRemoval(oldPath, removeTrees, removeEntries);
                MarkParent(oldPath, reconcileParents);
            }

            if (!string.IsNullOrWhiteSpace(newPath))
            {
                MarkUpsert(newPath, upsertTrees, upsertEntries, reconcileParents);
                MarkParent(newPath, reconcileParents);
            }
        }

        foreach (var e in events.Where(x => x is not RenamedEventArgs))
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    MarkRemoval(e.FullPath, removeTrees, removeEntries);
                    MarkParent(e.FullPath, reconcileParents);
                    break;
                case WatcherChangeTypes.Created:
                    MarkUpsert(e.FullPath, upsertTrees, upsertEntries, reconcileParents);
                    MarkParent(e.FullPath, reconcileParents);
                    break;
                case WatcherChangeTypes.Changed:
                    if (File.Exists(e.FullPath))
                        touchEntries.Add(e.FullPath);
                    else
                        MarkParent(e.FullPath, reconcileParents);
                    break;
            }
        }

        removeEntries.ExceptWith(upsertEntries);
        removeTrees.ExceptWith(upsertTrees);

        touchEntries.ExceptWith(removeEntries);
        touchEntries.ExceptWith(removeTrees);

        return new FsPlan(removeTrees, removeEntries, upsertTrees, upsertEntries, touchEntries, reconcileParents);
    }

    private async Task ApplyPlanAsync(FsPlan plan)
    {
        foreach (var path in plan.TouchEntries.Concat(plan.UpsertEntries))
            await HandleFileTouchAsync(path);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var tree in plan.RemoveTrees)
                RemoveLoadedTree(tree);

            foreach (var entry in plan.RemoveEntries)
                RemoveLoadedEntry(entry);
        });

        foreach (var tree in plan.UpsertTrees)
            await UpsertPathAsync(tree);

        foreach (var entry in plan.UpsertEntries)
            await UpsertPathAsync(entry);

        foreach (var parent in plan.ReconcileParents)
            await ReconcileFolderAsync(parent);
    }

    private async Task HandleFileTouchAsync(string path)
    {
        try
        {
            var openTab = _mainDockService.OpenFiles
                .FirstOrDefault(x => x.Key.EqualPaths(path));

            if (openTab.Value != null && File.Exists(path))
            {
                var lastWriteTime = File.GetLastWriteTime(path);
                if (lastWriteTime > openTab.Value.LastSaveTime)
                    await Dispatcher.UIThread.InvokeAsync(openTab.Value.InitializeContent);
            }

            var relativePath = SafeRelative(path);
            if (relativePath != null && _root.GetLoadedEntry(relativePath) is IProjectRootWithFile project)
            {
                if (File.Exists(project.FullPath))
                {
                    var lastWrite = File.GetLastWriteTime(project.FullPath);
                    if (lastWrite > project.LastSaveTime)
                        await _projectExplorerService.ReloadProjectAsync(project);
                }
            }

            if (_root.ProjectPath.EqualPaths(path) && !File.Exists(path))
                await Dispatcher.UIThread.InvokeAsync(() => _root.LoadingFailed = true);
        }
        catch
        {
        }
    }

    private void RemoveLoadedEntry(string fullPath)
    {
        var relativePath = SafeRelative(fullPath);
        if (relativePath == null || string.IsNullOrEmpty(relativePath)) return;

        var entry = _root.GetLoadedEntry(relativePath);
        entry?.TopFolder?.Remove(entry);
    }

    private void RemoveLoadedTree(string fullPath)
    {
        RemoveLoadedEntry(fullPath);
    }

    private async Task UpsertPathAsync(string fullPath)
    {
        var relativePath = SafeRelative(fullPath);
        if (relativePath == null) return;
        if (!_root.IsPathIncluded(relativePath)) return;

        if (_root.GetLoadedEntry(relativePath) != null) return;
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return;

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            EnsureParentChain(relativePath);

            var parentRel = Path.GetDirectoryName(relativePath);
            var parentFolder = string.IsNullOrWhiteSpace(parentRel)
                ? _root as IProjectFolder
                : _root.GetLoadedEntry(parentRel) as IProjectFolder;

            if (parentFolder == null) return;

            parentFolder.IsExpanded = true;

            if (attributes.HasFlag(FileAttributes.Directory))
                _root.AddFolder(relativePath);
            else
                _root.AddFile(relativePath);
        });
    }

    private void EnsureParentChain(string relativePath)
    {
        var parentRel = Path.GetDirectoryName(relativePath);
        if (string.IsNullOrWhiteSpace(parentRel)) return;

        var stack = new Stack<string>();
        var current = parentRel;

        while (!string.IsNullOrWhiteSpace(current) && _root.GetLoadedEntry(current) is not IProjectFolder)
        {
            stack.Push(current);
            current = Path.GetDirectoryName(current);
        }

        while (stack.Count > 0)
        {
            var folderRel = stack.Pop();
            var fullFolderPath = Path.Combine(_root.RootFolderPath, folderRel);
            if (!Directory.Exists(fullFolderPath)) continue;
            _root.AddFolder(folderRel);
        }
    }

    private async Task ReconcileFolderAsync(string fullParentPath)
    {
        var parentRel = SafeRelative(fullParentPath);
        if (parentRel == null) return;

        if (_root.GetLoadedEntry(parentRel) is not IProjectFolder parentEntry || !parentEntry.IsExpanded) return;

        string[] diskDirs;
        string[] diskFiles;
        try
        {
            if (!Directory.Exists(fullParentPath)) return;
            diskDirs = Directory.GetDirectories(fullParentPath);
            diskFiles = Directory.GetFiles(fullParentPath);
        }
        catch
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var children = parentEntry.Children?.OfType<IProjectEntry>().ToList() ?? [];

            foreach (var child in children)
            {
                var childFull = Path.Combine(_root.RootFolderPath, child.RelativePath);
                if (!File.Exists(childFull) && !Directory.Exists(childFull))
                    parentEntry.Remove(child);
            }

            foreach (var dir in diskDirs)
            {
                var rel = SafeRelative(dir);
                if (rel == null || !_root.IsPathIncluded(rel)) continue;
                if (_root.GetLoadedEntry(rel) == null)
                    _root.AddFolder(rel);
            }

            foreach (var file in diskFiles)
            {
                var rel = SafeRelative(file);
                if (rel == null || !_root.IsPathIncluded(rel)) continue;
                if (_root.GetLoadedEntry(rel) == null)
                    _root.AddFile(rel);
            }
        });
    }

    private Task FullResyncAsync()
    {
        return ReconcileFolderAsync(_root.RootFolderPath);
    }

    private void MarkParent(string fullPath, HashSet<string> parents)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir)) parents.Add(dir);
    }

    private void MarkRemoval(string fullPath, HashSet<string> removeTrees, HashSet<string> removeEntries)
    {
        var relativePath = SafeRelative(fullPath);
        if (relativePath == null) return;

        var loaded = _root.GetLoadedEntry(relativePath);
        if (loaded is IProjectFolder) removeTrees.Add(fullPath);
        else removeEntries.Add(fullPath);
    }

    private void MarkUpsert(string fullPath, HashSet<string> upsertTrees, HashSet<string> upsertEntries,
        HashSet<string> reconcileParents)
    {
        if (Directory.Exists(fullPath))
            upsertTrees.Add(fullPath);
        else if (File.Exists(fullPath))
            upsertEntries.Add(fullPath);
        else
            MarkParent(fullPath, reconcileParents);
    }

    private string? SafeRelative(string fullPath)
    {
        try
        {
            if (!fullPath.StartsWith(_root.RootFolderPath, StringComparison.OrdinalIgnoreCase))
                return null;
            return Path.GetRelativePath(_root.RootFolderPath, fullPath);
        }
        catch
        {
            return null;
        }
    }

    private bool IsHiddenPath(string fullPath)
    {
        var relative = SafeRelative(fullPath);
        if (string.IsNullOrWhiteSpace(relative)) return false;

        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in segments)
            if (segment.StartsWith('.'))
                return true;

        return false;
    }

    private sealed record FsPlan(
        HashSet<string> RemoveTrees,
        HashSet<string> RemoveEntries,
        HashSet<string> UpsertTrees,
        HashSet<string> UpsertEntries,
        HashSet<string> TouchEntries,
        HashSet<string> ReconcileParents
    );
}
