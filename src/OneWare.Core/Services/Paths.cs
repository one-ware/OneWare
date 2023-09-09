using System.Diagnostics;
using System.Runtime.InteropServices;
using OneWare.Shared.Extensions;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Core.Services;

public class Paths : IPaths
{
    public string AppName { get; }
    public string AppIconPath { get; }
    public string AppFolderName { get; }
    public string AppDataDirectory => 
        Path.Combine(Environment.GetFolderPath(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Environment.SpecialFolder.LocalApplicationData : Environment.SpecialFolder.ApplicationData), AppFolderName);
    public string TempDirectory => Path.GetTempPath();
    
    public string SessionDirectory { get; }
    public string LayoutDirectory => Path.Combine(AppDataDirectory, "Layouts");
    public string SettingsPath => Path.Combine(AppDataDirectory, "Settings.json");
    public string DocumentsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppFolderName);
    public string ProjectsDirectory => Path.Combine(DocumentsDirectory, "Projects");
    public string CrashReportsDirectory => Path.Combine(DocumentsDirectory, "CrashReports");
    public string ModulesPath => Path.Combine(DocumentsDirectory, "Modules");
    public string ChangelogUrl => "https://raw.githubusercontent.com/VHDPlus/vhdplus-website/master/docs/ide/changelog.md";
    
    public Paths(string appName, string appIconPath)
    {
        AppName = appName;
        AppIconPath = appIconPath;
        AppFolderName = appName.Replace(" ", "");

        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
        Directory.CreateDirectory(ModulesPath);
        Directory.CreateDirectory(CrashReportsDirectory);
        Directory.CreateDirectory(ProjectsDirectory);
        //...
        
        var sessionsDir = Path.Combine(TempDirectory, "OneWare", "Sessions");
        CleanupSessions(sessionsDir);
        
        SessionDirectory = Path.Combine(sessionsDir, "OneWareStudioSession").CheckNameDirectory();
        Directory.CreateDirectory(SessionDirectory);
        
        //Lock file
        File.Create(Path.Combine(SessionDirectory, ".session_lock"));
    }
    
    private static void CleanupSessions(string sessionsDir)
    {
        //Cleanup
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
                        using (var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            stream.Close();
                        }
                        Directory.Delete(session, true);
                    }
                    catch(Exception e)
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