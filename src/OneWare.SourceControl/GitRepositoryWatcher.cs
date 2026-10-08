namespace OneWare.SourceControl;

/// <summary>
///     Watches a repository's working tree and git directory and reports relevant changes once they settle,
///     so the Source Control view can refresh on demand instead of polling.
/// </summary>
public sealed class GitRepositoryWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(3);

    private static readonly string[] GitDirectoryFiles =
        ["HEAD", "index", "packed-refs", "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "config"];

    private readonly Action _changed;
    private readonly string _gitDirectory;
    private readonly object _lock = new();
    private readonly Timer _timer;
    private readonly List<FileSystemWatcher> _watchers = [];
    private bool _disposed;
    private DateTime? _firstPending;
    private DateTime _knownIndexWrite;

    public GitRepositoryWatcher(string? workingDirectory, string gitDirectory, Action changed)
    {
        _gitDirectory = Normalize(gitDirectory);
        _changed = changed;
        _timer = new Timer(_ => Flush());

        try
        {
            if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
                AddWatcher(workingDirectory);

            if (Directory.Exists(_gitDirectory) && !IsInside(_gitDirectory, workingDirectory))
                AddWatcher(_gitDirectory);

            IsActive = _watchers.Count > 0;
        }
        catch
        {
            // Too many watches (inotify limit) or unsupported file system, callers fall back to polling.
            DisposeWatchers();
            IsActive = false;
        }

        MarkRefreshed();
    }

    /// <summary>False when the file system cannot be watched, callers should poll instead.</summary>
    public bool IsActive { get; private set; }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _timer.Dispose();
        DisposeWatchers();
    }

    /// <summary>
    ///     Remembers the current index timestamp, reading the status must not trigger another refresh.
    /// </summary>
    public void MarkRefreshed()
    {
        _knownIndexWrite = GetIndexWriteTime();
    }

    private void AddWatcher(string path)
    {
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = 64 * 1024,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite |
                           NotifyFilters.Size
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;
        _watchers.Add(watcher);
        watcher.EnableRaisingEvents = true;
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsRelevant(e.OldFullPath) || IsRelevant(e.FullPath)) Schedule();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsRelevant(e.FullPath)) Schedule();
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Buffer overflow means events were lost, refresh to be safe.
        Schedule();
    }

    private bool IsRelevant(string fullPath)
    {
        var path = Normalize(fullPath);
        if (!IsInside(path, _gitDirectory)) return !IsNestedGitDirectory(path);

        var relative = path.Length > _gitDirectory.Length ? path[(_gitDirectory.Length + 1)..] : string.Empty;
        if (relative.Length == 0 || relative.EndsWith(".lock", StringComparison.Ordinal)) return false;
        if (relative.StartsWith("refs/", StringComparison.Ordinal)) return true;
        if (relative == "index") return GetIndexWriteTime() != _knownIndexWrite;
        return GitDirectoryFiles.Contains(relative);
    }

    private static bool IsNestedGitDirectory(string path)
    {
        return path.Contains("/.git/", StringComparison.Ordinal) || path.EndsWith("/.git", StringComparison.Ordinal);
    }

    private void Schedule()
    {
        lock (_lock)
        {
            if (_disposed) return;
            var now = DateTime.UtcNow;
            _firstPending ??= now;
            var delay = _firstPending.Value + MaxDelay - now;
            if (delay > Debounce) delay = Debounce;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _firstPending = null;
        }

        _changed();
    }

    private DateTime GetIndexWriteTime()
    {
        try
        {
            return File.GetLastWriteTimeUtc(Path.Combine(_gitDirectory, "index"));
        }
        catch
        {
            return default;
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private static bool IsInside(string path, string? directory)
    {
        if (string.IsNullOrEmpty(directory)) return false;
        directory = Normalize(directory);
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return path.Equals(directory, comparison) ||
               path.StartsWith(directory + "/", comparison);
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
    }
}
