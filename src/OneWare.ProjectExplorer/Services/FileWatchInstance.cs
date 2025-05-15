﻿using Avalonia.Threading;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchInstance : IDisposable
{
    private readonly List<FileSystemEventArgs> _changes = new();
    private readonly IDockService _dockService;
    private readonly IFile _file;
    private readonly FileSystemWatcher? _fileSystemWatcher;
    private readonly object _lock = new();
    private readonly IWindowService _windowService;
    private readonly ILogger _logger;
    private DispatcherTimer? _timer;

    public FileWatchInstance(
        IFile file,
        IDockService dockService,
        ISettingsService settingsService,
        IWindowService windowService,
        ILogger logger)
    {
        _file = file;
        _dockService = dockService;
        _windowService = windowService;
        _logger = logger;

        if (!File.Exists(file.FullPath)) return;

        try
        {
            _fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(file.FullPath)!)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                Filter = Path.GetFileName(file.FullPath)
            };

            _fileSystemWatcher.Changed += File_Changed;
            _fileSystemWatcher.Deleted += File_Changed;
            _fileSystemWatcher.Renamed += File_Changed;
            _fileSystemWatcher.Created += File_Changed;

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
            _logger.Error(e.Message, e);
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _fileSystemWatcher?.Dispose();
    }

    private void File_Changed(object source, FileSystemEventArgs e)
    {
        if (e.Name == null || e.FullPath != _file.FullPath) return;

        lock (_lock)
        {
            _changes.Add(e);
        }
    }

    private void ProcessChanges()
    {
        if (_changes.Any())
        {
            Process(_changes);
        }

        _changes.Clear();
    }

    private void Process(IReadOnlyCollection<FileSystemEventArgs> changes)
    {
        try
        {
            var lastArg = changes.Last();

            _dockService.OpenFiles.TryGetValue(_file, out var tab);

            if (tab == null)
            {
                return;
            }

            switch (lastArg.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Renamed:
                case WatcherChangeTypes.Changed:
                    if (File.GetLastWriteTime(_file.FullPath) > _file.LastSaveTime)
                        tab.InitializeContent();
                    return;

                case WatcherChangeTypes.Deleted:
                    tab.InitializeContent();
                    return;
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e, false);
        }
    }
}
