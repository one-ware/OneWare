using System.Diagnostics;
using System.Runtime.InteropServices;
using ImTools;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class Paths : IPaths
{
    private FileStream? _fileStreamLock;

    public Paths()
    {
        // Corrected the issue by replacing the erroneous method call with a constructor chaining call.
        // This ensures the initialization logic is properly invoked.
        AppName = "OneWare Studio";
        AppIconPath = "avares://OneWare.Core/Assets/Icons/OneWareStudioIcon.png";
        AppFolderName = AppName.Replace(" ", "");

        var commandLineArgs = Environment.GetCommandLineArgs();
        if (commandLineArgs.Length > 1 && commandLineArgs.IndexOf(x => x == "--oneware-dir") is { } dirIndex and >= 0)
        {
            DocumentsDirectory = Path.GetFullPath(commandLineArgs[dirIndex + 1]);
        }
        else DocumentsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppFolderName);
        if (commandLineArgs.Length > 1 && commandLineArgs.IndexOf(x => x == "--oneware-appdata-dir") is { } appdataDirIndex and >= 0)
        {
            AppDataDirectory = Path.GetFullPath(commandLineArgs[appdataDirIndex + 1]);
        }
        else AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Environment.SpecialFolder.LocalApplicationData
                : Environment.SpecialFolder.ApplicationData), AppFolderName);

        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
        Directory.CreateDirectory(PackagesDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(NativeToolsDirectory);
        Directory.CreateDirectory(CrashReportsDirectory);
        Directory.CreateDirectory(ProjectsDirectory);
        //...

        var sessionsDir = Path.Combine(TempDirectory, "OneWare", "Sessions");
        CleanupSessions(sessionsDir);

        SessionDirectory = Path.Combine(sessionsDir, "OneWareStudioSession").CheckNameDirectory();
        Directory.CreateDirectory(SessionDirectory);

        //Lock file
        _fileStreamLock = new FileStream(Path.Combine(SessionDirectory, ".session_lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }
    public Paths(string appName, string appIconPath)
    {
        AppName = appName;
        AppIconPath = appIconPath;
        AppFolderName = appName.Replace(" ", "");
        
        var commandLineArgs = Environment.GetCommandLineArgs();
        if (commandLineArgs.Length > 1 && commandLineArgs.IndexOf(x => x == "--oneware-dir") is { } dirIndex and >= 0)
        {
            DocumentsDirectory = Path.GetFullPath(commandLineArgs[dirIndex + 1]);
        }
        else DocumentsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppFolderName);
        if (commandLineArgs.Length > 1 && commandLineArgs.IndexOf(x => x == "--oneware-appdata-dir") is { } appdataDirIndex and >= 0)
        {
            AppDataDirectory = Path.GetFullPath(commandLineArgs[appdataDirIndex + 1]);
        }
        else AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Environment.SpecialFolder.LocalApplicationData
                : Environment.SpecialFolder.ApplicationData), AppFolderName);

        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
        Directory.CreateDirectory(PackagesDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(NativeToolsDirectory);
        Directory.CreateDirectory(CrashReportsDirectory);
        Directory.CreateDirectory(ProjectsDirectory);
        //...

        var sessionsDir = Path.Combine(TempDirectory, "OneWare", "Sessions");
        CleanupSessions(sessionsDir);

        SessionDirectory = Path.Combine(sessionsDir, "OneWareStudioSession").CheckNameDirectory();
        Directory.CreateDirectory(SessionDirectory);

        //Lock file
        _fileStreamLock = new FileStream(Path.Combine(SessionDirectory, ".session_lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }

    public string AppName { get; }
    public string AppIconPath { get; }
    public string AppFolderName { get; }

    public string AppDataDirectory { get; }

    public string TempDirectory => Path.GetTempPath();

    public string SessionDirectory { get; }
    public string LayoutDirectory => Path.Combine(AppDataDirectory, "Layouts");
    public string SettingsPath => Path.Combine(AppDataDirectory, "Settings.json");

    public string DocumentsDirectory { get; }

    public string ProjectsDirectory => Path.Combine(DocumentsDirectory, "Projects");
    public string CrashReportsDirectory => Path.Combine(DocumentsDirectory, "CrashReports");
    public string PackagesDirectory => Path.Combine(DocumentsDirectory, "Packages");
    public string NativeToolsDirectory => Path.Combine(PackagesDirectory, "NativeTools");
    public string PluginsDirectory => Path.Combine(PackagesDirectory, "Plugins");

    public string ChangelogUrl =>
        "https://raw.githubusercontent.com/one-ware/one-ware.com/main/docs/studio/02-changelog.md";

    public string UpdateInfoUrl => "https://cdn.one-ware.com/onewarestudio";

    private static void CleanupSessions(string sessionsDir)
    {
        try
        {
            if (Directory.Exists(sessionsDir))
            {
                var sessionFolders = Directory.GetDirectories(sessionsDir);

                foreach (var session in sessionFolders)
                {
                    var lockFilePath = Path.Combine(session, ".session_lock");
                    var fileInfo = new FileInfo(lockFilePath);

                    try
                    {
                        if (fileInfo.Exists)
                        {
                            using var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                            stream.Close();
                        }

                        Directory.Delete(session, true);
                    }
                    catch (Exception e)
                    {
                        Debug.Write(e.Message);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.Write(e.Message);
        }
    }
}