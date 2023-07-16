using System.Security.Cryptography;
using Avalonia.Threading;
using OneWare.Shared;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Services;

public class FileWatchInstance : IDisposable
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly IProjectRoot _root;
    private readonly FileSystemWatcher _fileSystemWatcher;

    public FileWatchInstance(IProjectRoot root, IProjectExplorerService projectExplorerService, ISettingsService settingsService, ILogger logger)
    {
        _root = root;
        _projectExplorerService = projectExplorerService;
        _settingsService = settingsService;
        _logger = logger;
        
        _fileSystemWatcher = new FileSystemWatcher(root.FullPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true
        };
        
        _fileSystemWatcher.Changed += File_Changed;
        _fileSystemWatcher.Deleted += File_Deleted;
        _fileSystemWatcher.Renamed += File_Renamed;
        _fileSystemWatcher.Created += File_Created;

        try
        {
            _settingsService.GetSettingObservable<bool>("Editor_DetectExternalChanges").Subscribe(x =>
            {
                _fileSystemWatcher.EnableRaisingEvents = true;
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
        Dispatcher.UIThread.Post(() =>
        {
            if (_root.Search(e.FullPath) is { } entry)
            {
                Console.WriteLine(CalculateMD5(entry.FullPath));
                //_projectExplorerService.ReloadAsync(entry);
            }
        });
    }
    
    static string CalculateMD5(string filename)
    {
        using var md5 = MD5.Create();
        using (var stream = File.OpenRead(filename))
        {
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    private void File_Deleted(object source, FileSystemEventArgs e)
    {
        /*e.ChangeType 
        
        if (e.Name == null) return; 
        Dispatcher.UIThread.Post(() =>
        {
            if (_watcherHandle.Contains(e.Name)) return;
            _watcherHandle.Add(e.Name);
            if (Search(e.Name) is { } entry)
            {
                entry.LoadingFailed = true;
            }

            _watcherHandle.Remove(e.Name);
        }, DispatcherPriority.Background);*/
    }

    private void File_Created(object source, FileSystemEventArgs e)
    {
        /*if (e.Name == null) return;
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_watcherHandle.Contains(e.Name)) return;
            _watcherHandle.Add(e.Name);
            if (Search(e.Name) is { } entry && entry.LoadingFailed)
                entry.LoadingFailed = false;
            _watcherHandle.Remove(e.Name);
        }, DispatcherPriority.Background);*/
    }

    private void File_Renamed(object source, RenamedEventArgs e)
    {
        /*if (e.Name == null) return;
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (e.OldName != null && Search(e.OldName) is { } entry)
            {
                entry.Header = e.Name;
                if (_watcherHandle.Contains(e.Name)) return;

                if (entry.LoadingFailed)
                {
                    _watcherHandle.Add(e.Name);
                    await ContainerLocator.Container.Resolve<IProjectExplorerService>(e.FullPath).HandleFileChangeAsync(e.FullPath);
                    _watcherHandle.Remove(e.Name);
                }
            }
        }, DispatcherPriority.Background);*/
    }

    public void Dispose()
    {
        _fileSystemWatcher.Dispose();
    }
}