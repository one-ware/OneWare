using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

/// <summary>
///     Persistent copy cache for directories that contain loadable binaries.
///     Layout: <c>{CacheDirectory}/Binaries/{category}/{name}/{stamp}/{sourceFolderName}</c>.
///     The stamp is derived from the relative paths, sizes and modification times of all source files,
///     so a new copy is only made when the source changes. Running instances hold a lock file inside
///     the entry they use, which prevents cleanup from removing it.
/// </summary>
public sealed class BinaryCacheService : IBinaryCacheService, IDisposable
{
    private const string CompleteMarkerFileName = ".complete";
    private const string LocksDirectoryName = ".locks";
    private const string TempPrefix = ".tmp-";
    private const string DeletingPrefix = ".deleting-";

    private readonly Dictionary<string, string> _acquiredCopies = new(StringComparer.Ordinal);
    private readonly HashSet<string> _acquiredEntries = new(StringComparer.Ordinal);
    private readonly List<FileStream> _locks = new();
    private readonly ILogger _logger;
    private readonly string _rootDirectory;
    private readonly Lock _sync = new();

    public BinaryCacheService(IPaths paths, ILogger logger) : this(Path.Combine(paths.CacheDirectory, "Binaries"),
        logger)
    {
    }

    internal BinaryCacheService(string rootDirectory, ILogger logger)
    {
        _logger = logger;
        _rootDirectory = rootDirectory;
    }

    public string? GetOrCreateCopy(string category, string name, string sourceDirectory)
    {
        try
        {
            var sourceRoot = Path.GetFullPath(sourceDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(sourceRoot)) return null;

            var nameDirectory = Path.Combine(_rootDirectory, SanitizeName(category), SanitizeName(name));
            var requestKey = nameDirectory + "|" + sourceRoot;

            lock (_sync)
            {
                if (_acquiredCopies.TryGetValue(requestKey, out var existing) && Directory.Exists(existing))
                    return existing;
            }

            var entryDirectory = Path.Combine(nameDirectory, ComputeStamp(sourceRoot));
            var contentDirectory = Path.Combine(entryDirectory, Path.GetFileName(sourceRoot));

            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (IsComplete(entryDirectory))
                {
                    AcquireEntry(entryDirectory);

                    // Re-check after acquiring the lock in case another instance removed the entry meanwhile
                    if (IsComplete(entryDirectory) && Directory.Exists(contentDirectory))
                    {
                        lock (_sync)
                        {
                            _acquiredCopies[requestKey] = contentDirectory;
                        }

                        return contentDirectory;
                    }

                    continue;
                }

                CreateEntry(sourceRoot, nameDirectory, entryDirectory);
            }

            _logger.LogWarning("Could not create cached copy of {Source}", sourceRoot);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not create cached copy of {Source}", sourceDirectory);
        }

        return null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var lockStream in _locks) lockStream.Dispose();
            _locks.Clear();
            _acquiredEntries.Clear();
            _acquiredCopies.Clear();
        }
    }

    public void CleanupUnusedEntries()
    {
        if (!Directory.Exists(_rootDirectory)) return;

        foreach (var categoryDirectory in SafeGetDirectories(_rootDirectory))
        foreach (var nameDirectory in SafeGetDirectories(categoryDirectory))
        {
            foreach (var entryDirectory in SafeGetDirectories(nameDirectory))
                try
                {
                    CleanupEntry(entryDirectory);
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Failed to clean up binary cache entry {Entry}", entryDirectory);
                }

            TryDeleteIfEmpty(nameDirectory);
        }
    }

    private void CreateEntry(string sourceRoot, string nameDirectory, string entryDirectory)
    {
        Directory.CreateDirectory(nameDirectory);

        // Leftover from an interrupted run: entries are only moved into place once complete
        if (Directory.Exists(entryDirectory) && !IsComplete(entryDirectory) && !IsInUse(entryDirectory))
            TryDeleteDirectory(entryDirectory);

        var tempDirectory = Path.Combine(nameDirectory, $"{TempPrefix}{Environment.ProcessId}-{Guid.NewGuid():N}");
        try
        {
            var stopwatch = Stopwatch.StartNew();

            PlatformHelper.CopyDirectory(sourceRoot, Path.Combine(tempDirectory, Path.GetFileName(sourceRoot)));
            File.WriteAllText(Path.Combine(tempDirectory, CompleteMarkerFileName), sourceRoot);

            Directory.Move(tempDirectory, entryDirectory);

            _logger.LogInformation("Cached copy of {Source} created in {Milliseconds} ms", sourceRoot,
                stopwatch.ElapsedMilliseconds);
        }
        catch (IOException) when (IsComplete(entryDirectory))
        {
            // Another instance created the same entry first
        }
        finally
        {
            if (Directory.Exists(tempDirectory)) TryDeleteDirectory(tempDirectory);
        }
    }

    private void AcquireEntry(string entryDirectory)
    {
        lock (_sync)
        {
            if (_acquiredEntries.Contains(entryDirectory)) return;
        }

        var locksDirectory = Path.Combine(entryDirectory, LocksDirectoryName);
        Directory.CreateDirectory(locksDirectory);

        var lockStream = new FileStream(
            Path.Combine(locksDirectory, $"{Environment.ProcessId}-{Guid.NewGuid():N}.lock"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);

        lock (_sync)
        {
            _locks.Add(lockStream);
            _acquiredEntries.Add(entryDirectory);
        }
    }

    private void CleanupEntry(string entryDirectory)
    {
        var entryName = Path.GetFileName(entryDirectory);

        if (entryName.StartsWith(DeletingPrefix, StringComparison.Ordinal))
        {
            TryDeleteDirectory(entryDirectory);
            return;
        }

        if (entryName.StartsWith(TempPrefix, StringComparison.Ordinal))
        {
            if (!IsProcessRunning(entryName)) TryDeleteDirectory(entryDirectory);
            return;
        }

        lock (_sync)
        {
            if (_acquiredEntries.Contains(entryDirectory)) return;
        }

        if (IsComplete(entryDirectory))
        {
            var sourceRoot = File.ReadAllText(Path.Combine(entryDirectory, CompleteMarkerFileName)).Trim();
            if (Directory.Exists(sourceRoot) &&
                string.Equals(ComputeStamp(sourceRoot), entryName, StringComparison.Ordinal))
                return;
        }

        if (IsInUse(entryDirectory)) return;

        // Rename first so no other instance picks up a partially deleted entry.
        // On Windows this fails while binaries from the entry are loaded, which keeps the entry alive.
        var deletingDirectory = Path.Combine(Path.GetDirectoryName(entryDirectory)!,
            $"{DeletingPrefix}{Guid.NewGuid():N}");
        Directory.Move(entryDirectory, deletingDirectory);
        TryDeleteDirectory(deletingDirectory);

        _logger.LogDebug("Removed outdated binary cache entry {Entry}", entryDirectory);
    }

    private static bool IsInUse(string entryDirectory)
    {
        var locksDirectory = Path.Combine(entryDirectory, LocksDirectoryName);
        if (!Directory.Exists(locksDirectory)) return false;

        var inUse = false;
        foreach (var lockFile in Directory.GetFiles(locksDirectory))
            try
            {
                using (new FileStream(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }

                File.Delete(lockFile);
            }
            catch (FileNotFoundException)
            {
            }
            catch (IOException)
            {
                inUse = true;
            }
            catch (UnauthorizedAccessException)
            {
                inUse = true;
            }

        return inUse;
    }

    private static bool IsProcessRunning(string tempDirectoryName)
    {
        var pidPart = tempDirectoryName[TempPrefix.Length..].Split('-')[0];
        if (!int.TryParse(pidPart, out var pid)) return false;
        if (pid == Environment.ProcessId) return true;

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsComplete(string entryDirectory)
    {
        return File.Exists(Path.Combine(entryDirectory, CompleteMarkerFileName));
    }

    /// <summary>
    ///     Cheap fingerprint of a directory tree based on file metadata only.
    /// </summary>
    internal static string ComputeStamp(string sourceRoot)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0
        };

        var files = new DirectoryInfo(sourceRoot).EnumerateFiles("*", options)
            .Select(file => (RelativePath: Path.GetRelativePath(sourceRoot, file.FullName).Replace('\\', '/'), File: file))
            .OrderBy(x => x.RelativePath, StringComparer.Ordinal);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (relativePath, file) in files)
            hash.AppendData(Encoding.UTF8.GetBytes(
                $"{relativePath}\0{file.Length}\0{file.LastWriteTimeUtc.Ticks}\n"));

        return Convert.ToHexString(hash.GetHashAndReset(), 0, 12).ToLowerInvariant();
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(sanitized) || sanitized.StartsWith('.') ? "_" + sanitized : sanitized;
    }

    private static IEnumerable<string> SafeGetDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void TryDeleteIfEmpty(string directory)
    {
        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Failed to delete {Directory}", directory);
        }
    }
}
