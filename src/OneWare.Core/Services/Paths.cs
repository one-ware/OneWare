using System.Runtime.InteropServices;
using OneWare.Shared.Services;

namespace OneWare.Core.Services;

public class Paths : IPaths
{
    public string AppName { get; }
    public string AppIconPath { get; }
    public string AppFolderName { get; }
    public string AppDataDirectory => 
        Path.Combine(Environment.GetFolderPath(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Environment.SpecialFolder.LocalApplicationData : Environment.SpecialFolder.ApplicationData), AppFolderName);
    public string TempDirectory => Path.GetTempPath();
    public string LayoutDirectory => Path.Combine(AppDataDirectory, "Layouts");
    public string SettingsPath => Path.Combine(AppDataDirectory, "Settings.json");
    public string DocumentsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppFolderName);
    public string PackagesDirectory => Path.Combine(DocumentsDirectory, "Packages");
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
        Directory.CreateDirectory(PackagesDirectory);
        //...
    }
}