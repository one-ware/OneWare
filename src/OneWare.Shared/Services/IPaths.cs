namespace OneWare.Shared.Services;

public interface IPaths
{
    public string AppName { get; }
    public string AppIconPath { get; }
    public string SplashScreenPath { get; }
    public string AppFolderName { get; }
    public string AppDataDirectory { get; }
    public string LayoutDirectory { get; }
    public string PackagesDirectory { get; }
    public string LibrariesDirectory { get; }
    public string CustomFpgaDirectory { get; }
    public string OfficialFpgaDirectory { get; }
    public string SettingsPath { get; }
    public string DocumentsDirectory { get; }
    public string CrashReportsDirectory { get; }
    public string ProjectsDirectory { get; }
    public string ModulesPath { get; }
}