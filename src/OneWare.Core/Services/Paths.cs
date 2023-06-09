using System.Runtime.InteropServices;
using OneWare.Shared.Services;

namespace OneWare.Core.Services;

public class Paths : IPaths
{
    public string AppName { get; }
    public string AppIconPath { get; }
    public string SplashScreenPath { get; }
    public string AppFolderName { get; }
    
    public string AppDataDirectory => 
        Path.Combine(Environment.GetFolderPath(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Environment.SpecialFolder.LocalApplicationData : Environment.SpecialFolder.ApplicationData), AppFolderName);
    public string TempDirectory => Path.GetTempPath();
    public string LayoutDirectory => Path.Combine(AppDataDirectory, "Layouts");
    public string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");
    public string DocumentsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppFolderName);
    public string PackagesDirectory => Path.Combine(DocumentsDirectory, "Packages");
    public string ProjectsDirectory => Path.Combine(DocumentsDirectory, "Projects");
    public string LibrariesDirectory => Path.Combine(DocumentsDirectory, "Libraries");
    public string CrashReportsDirectory => Path.Combine(DocumentsDirectory, "CrashReports");
    public string CustomFpgaDirectory => Path.Combine(DocumentsDirectory, "CustomFPGAs");
    public string ModulesPath => Path.Combine(DocumentsDirectory, "Modules");
    
    public Paths(string appName, string appIconPath, string splashScreenPath)
    {
        AppName = appName;
        AppIconPath = appIconPath;
        SplashScreenPath = splashScreenPath;
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