using OneWare.Essentials.Services;
using System;
using System.IO;

namespace OneWare.Essentials.Helpers;

public class FileWatcher : IDisposable
{
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly ILogger _logger;

    private DateTime _lastRead = DateTime.MinValue;

    public FileWatcher(string filePath, Action onChanged, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
    public static IDisposable? WatchFile(string path, Action onChanged, ILogger logger)
    {
        try
        {
            return new FileWatcher(path, onChanged, logger);
        }
        catch (Exception e)
        {
            logger?.Error(e.Message, e);
        }
        return null;
    }
}
