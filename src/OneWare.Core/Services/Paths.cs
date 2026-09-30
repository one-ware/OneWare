using System.Diagnostics;
using System.Runtime.InteropServices;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class Paths : IPaths
{
    private FileStream? _fileStreamLock;

    public Paths(string appName, string appIconPath)
    {
        AppName = appName;
        AppIconPath = appIconPath;
        AppFolderName = appName.Replace(" ", "");

        DocumentsDirectory = Environment.GetEnvironmentVariable("ONEWARE_DIR") ??
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                 AppFolderName);

        AppDataDirectory = Environment.GetEnvironmentVariable("ONEWARE_APPDATA_DIR") ??
                           Path.Combine(Environment.GetFolderPath(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                               ? Environment.SpecialFolder.LocalApplicationData
                               : Environment.SpecialFolder.ApplicationData), AppFolderName);

        CacheDirectory = Environment.GetEnvironmentVariable("ONEWARE_APPDATA_DIR") is { Length: > 0 }
            ? Path.Combine(AppDataDirectory, "Cache")
            : Path.Combine(GetPlatformCacheRoot(), AppFolderName);

        ProjectsDirectory = Environment.GetEnvironmentVariable("ONEWARE_PROJECTS_DIR")
                            ?? Path.Combine(DocumentsDirectory, "Projects");

        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
        Directory.CreateDirectory(PackagesDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(NativeToolsDirectory);
        Directory.CreateDirectory(OnnxRuntimesDirectory);
        Directory.CreateDirectory(CrashReportsDirectory);
        Directory.CreateDirectory(ProjectsDirectory);
        //...

        var sessionsDir = Path.Combine(TempDirectory, "OneWare", "Sessions");
        var previousSessions = Directory.Exists(sessionsDir) ? Directory.GetDirectories(sessionsDir) : [];

        SessionDirectory = Path.Combine(sessionsDir, "OneWareStudioSession").CheckNameDirectory();
        Directory.CreateDirectory(SessionDirectory);

        //Lock file
        _fileStreamLock = new FileStream(Path.Combine(SessionDirectory, ".session_lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None, 32, FileOptions.DeleteOnClose);

        // Deleting old sessions can take a while (they may contain large files), so do it off the startup path
        if (previousSessions.Length > 0) _ = Task.Run(() => CleanupSessions(previousSessions));
    }

    public string AppName { get; }
    public string AppIconPath { get; }
    public string AppFolderName { get; }

    public string AppDataDirectory { get; }

    public string TempDirectory => Path.GetTempPath();

    public string SessionDirectory { get; }
    public string CacheDirectory { get; }
    public string LayoutDirectory => Path.Combine(AppDataDirectory, "Layouts");
    public string SettingsPath => Path.Combine(AppDataDirectory, "Settings.json");

    public string DocumentsDirectory { get; }

    public string ProjectsDirectory { get; }

    public string CrashReportsDirectory => Path.Combine(DocumentsDirectory, "CrashReports");
    public string PackagesDirectory => Path.Combine(DocumentsDirectory, "Packages");
    public string NativeToolsDirectory => Path.Combine(PackagesDirectory, "NativeTools");
    public string OnnxRuntimesDirectory => Path.Combine(PackagesDirectory, "OnnxRuntimes");
    public string PluginsDirectory => Path.Combine(PackagesDirectory, "Plugins");

    public string ChangelogUrl =>
        "https://raw.githubusercontent.com/one-ware/OneWare/refs/heads/main/docs/changelog.md";

    public string UpdateInfoUrl => "https://cdn.one-ware.com/onewarestudio";

    private static string GetPlatformCacheRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(home, "Library", "Caches");

        return Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdgCache
            ? xdgCache
            : Path.Combine(home, ".cache");
    }

    private static void CleanupSessions(IEnumerable<string> sessionFolders)
    {
        foreach (var session in sessionFolders)
        {
            try
            {
                var lockFilePath = Path.Combine(session, ".session_lock");
                var fileInfo = new FileInfo(lockFilePath);

                if (fileInfo.Exists)
                {
                    using var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                    stream.Close();
                }

                // Move the folder away first so a new session can never pick up a half deleted directory
                var deletePath = Path.Combine(Path.GetDirectoryName(session)!, $".deleting-{Guid.NewGuid():N}");
                Directory.Move(session, deletePath);
                Directory.Delete(deletePath, true);
            }
            catch (Exception e)
            {
                Debug.Write(e.Message);
            }
        }
    }
}
