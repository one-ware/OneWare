using System.Diagnostics;

namespace OneWare.Essentials.Helpers;

/// <summary>
/// Helpers for releasing executables that are still running, so their files can be replaced.
/// Overwriting a running executable fails with "Text file busy" on Unix and with a sharing
/// violation on Windows.
/// </summary>
public static class ProcessHelper
{
    private const int MaxSymlinkHops = 16;

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Kills every running process whose executable lives inside <paramref name="directory"/> and
    /// waits until the files can be written again.
    /// </summary>
    /// <param name="directory">Installation directory to release.</param>
    /// <param name="timeout">How long to wait for the operating system to release the files.</param>
    /// <returns><c>true</c> when every file in the directory can be written again.</returns>
    public static async Task<bool> ReleaseDirectoryAsync(string directory, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory)) return true;

        KillProcessesRunningFrom(directory);

        var busy = GetBusyFiles(directory);
        if (busy.Count == 0) return true;

        var deadline = DateTimeOffset.UtcNow + timeout;

        foreach (var file in busy)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) return false;

            if (!await WaitForFileReleaseAsync(file, remaining, cancellationToken)) return false;
        }

        return true;
    }

    /// <summary>
    /// Kills every running process whose executable lives inside <paramref name="directory"/>.
    /// </summary>
    /// <returns>The executable paths of the processes that were killed.</returns>
    public static IReadOnlyList<string> KillProcessesRunningFrom(string directory)
    {
        var prefixes = GetDirectoryPrefixes(directory);
        var killed = new List<string>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId) continue;

                var executable = TryGetExecutablePath(process);
                if (executable == null) continue;

                if (!IsInside(executable, prefixes)) continue;

                if (!process.HasExited) process.Kill(true);

                killed.Add(executable);
            }
            catch
            {
                // Process exited in the meantime, its modules are not accessible, or it cannot be
                // signalled by this user.
            }
            finally
            {
                process.Dispose();
            }
        }

        return killed;
    }

    /// <summary>
    /// Removes files that are still locked by a running process so that an installer can write
    /// fresh copies. Unix allows unlinking a running executable, Windows at least allows renaming
    /// it aside; in both cases the running process keeps using the old file.
    /// </summary>
    /// <returns>The files that could not be freed.</returns>
    public static IReadOnlyList<string> FreeBusyFiles(string directory)
    {
        if (!Directory.Exists(directory)) return [];

        DeleteStaleBackups(directory);

        var remaining = new List<string>();

        foreach (var file in GetBusyFiles(directory))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                try
                {
                    File.Move(file, $"{file}.old-{Guid.NewGuid():N}");
                }
                catch
                {
                    remaining.Add(file);
                }
            }
        }

        return remaining;
    }

    /// <summary>
    /// Polls until <paramref name="path"/> can be opened for writing, which is what an installer
    /// needs in order to replace it.
    /// </summary>
    public static async Task<bool> WaitForFileReleaseAsync(string path, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            if (!IsFileBusy(path)) return true;

            if (DateTimeOffset.UtcNow >= deadline) return false;

            await Task.Delay(150, cancellationToken);
        }
    }

    /// <summary>
    /// Resolves every symbolic link in <paramref name="path"/>, including links used by
    /// intermediate directories. <see cref="Path.GetFullPath(string)"/> does not do this, so two
    /// paths pointing at the same file can otherwise compare as different.
    /// </summary>
    public static string ResolveRealPath(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root)) return full;

            var current = root;

            foreach (var segment in full[root.Length..]
                         .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);

                for (var hop = 0; hop < MaxSymlinkHops; hop++)
                {
                    var target = new DirectoryInfo(current).LinkTarget ?? new FileInfo(current).LinkTarget;
                    if (target == null) break;

                    current = Path.GetFullPath(target, Path.GetDirectoryName(current) ?? root);
                }
            }

            return current;
        }
        catch
        {
            return Path.GetFullPath(path);
        }
    }

    private static string[] GetDirectoryPrefixes(string directory)
    {
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var real = ResolveRealPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return full.Equals(real, PathComparison) ? [full] : [full, real];
    }

    private static bool IsInside(string executable, string[] prefixes)
    {
        string[] candidates = [Path.GetFullPath(executable), ResolveRealPath(executable)];

        foreach (var candidate in candidates)
        foreach (var prefix in prefixes)
            if (candidate.StartsWith(prefix, PathComparison))
                return true;

        return false;
    }

    /// <summary>
    /// Determines the executable of a process. <see cref="Process.MainModule"/> is not readable for
    /// processes in a different mount namespace (a sandboxed sibling application for example), so
    /// the Linux process table is used as a fallback.
    /// </summary>
    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            var mainModule = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(mainModule)) return mainModule;
        }
        catch
        {
            // Not accessible, fall through.
        }

        if (!OperatingSystem.IsLinux()) return null;

        try
        {
            var link = new FileInfo($"/proc/{process.Id}/exe").LinkTarget;
            if (!string.IsNullOrEmpty(link)) return link;
        }
        catch
        {
            // Not accessible, fall through.
        }

        try
        {
            var command = File.ReadAllText($"/proc/{process.Id}/cmdline")
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(command) && command.Contains(Path.DirectorySeparatorChar)) return command;
        }
        catch
        {
            // Process exited in the meantime.
        }

        return null;
    }

    private static List<string> GetBusyFiles(string directory)
    {
        var busy = new List<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                if (IsFileBusy(file))
                    busy.Add(file);
        }
        catch
        {
            // Directory disappeared or is not enumerable.
        }

        return busy;
    }

    private static bool IsFileBusy(string path)
    {
        if (!File.Exists(path)) return false;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            // Not writable for this user at all — waiting or retrying will not change that.
            return false;
        }
    }

    private static void DeleteStaleBackups(string directory)
    {
        try
        {
            foreach (var backup in Directory.EnumerateFiles(directory, "*.old-*", SearchOption.AllDirectories))
                try
                {
                    File.Delete(backup);
                }
                catch
                {
                    // Still in use by a process that has not exited yet.
                }
        }
        catch
        {
            // Directory disappeared or is not enumerable.
        }
    }
}
