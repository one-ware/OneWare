using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchInstance : IDisposable
{
    private readonly List<FileSystemEventArgs> _changes = new();
    private readonly IExtendedDocument _document;
    private readonly FileSystemWatcher? _fileSystemWatcher;
    private readonly Lock _lock = new();
    private DispatcherTimer? _timer;

    public FileWatchInstance(IExtendedDocument document, ISettingsService settingsService, ILogger logger)
    {
        _document = document;

        if (!File.Exists(document.FullPath)) return;

        try
        {
            _fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(document.FullPath)!)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                Filter = Path.GetFileName(document.FullPath)
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
            logger.Error(e.Message, e);
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _fileSystemWatcher?.Dispose();
    }

    private void File_Changed(object source, FileSystemEventArgs e)
    {
        if (e.Name == null || !e.FullPath.EqualPaths(_document.FullPath)) return;
        lock (_lock)
        {
            _changes.Add(e);
        }
    }

    private void ProcessChanges()
    {
        if (_changes.Any()) Process(_changes);
        _changes.Clear();
    }

    private void Process(IReadOnlyCollection<FileSystemEventArgs> changes)
    {
        try
        {
            var lastArg = changes.Last();

            switch (lastArg.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Renamed:
                case WatcherChangeTypes.Changed:
                    if (File.GetLastWriteTime(_document.FullPath) > _document.LastSaveTime)
                        _document.InitializeContent();
                    return;
                case WatcherChangeTypes.Deleted:
                    _document.InitializeContent();
                    return;
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e, false);
        }
    }
}
