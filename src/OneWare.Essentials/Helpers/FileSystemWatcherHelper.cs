using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Essentials.Helpers;

public class FileWatcher : IDisposable
{
    private readonly FileSystemWatcher _fileSystemWatcher;
    
    private DateTime _lastRead = DateTime.MinValue;

    public FileWatcher(string filePath, Action onChanged)
    {
        _fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(filePath)!)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            Filter = Path.GetFileName(filePath)
        };

        _fileSystemWatcher.Changed += (_, _) =>
        {
            DateTime lastWriteTime = File.GetLastWriteTime(filePath);
            if (lastWriteTime != _lastRead)
            {
                onChanged.Invoke();
                _lastRead = lastWriteTime;
            }
        };
        
        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _fileSystemWatcher.Dispose();
    }
}

public static class FileSystemWatcherHelper
{
    public static IDisposable? WatchFile(string path, Action onChanged)
    {
        try
        {
            return new FileWatcher(path, onChanged);
        }
        catch (Exception e)
        {
            AppServices.Logger.LogError(e, e.Message);
        }
        return null;
    }
}