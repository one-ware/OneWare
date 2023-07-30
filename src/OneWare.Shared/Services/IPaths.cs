namespace OneWare.Shared.Services;

public interface IPaths
{
    public string AppName { get; }
    public string AppIconPath { get; }
    public string AppFolderName { get; }
    public string AppDataDirectory { get; }
    public string TempDirectory { get; }
    public string LayoutDirectory { get; }
    public string PackagesDirectory { get; }
    public string SettingsPath { get; }
    public string DocumentsDirectory { get; }
    public string CrashReportsDirectory { get; }
    public string ProjectsDirectory { get; }
    public string ModulesPath { get; }
    public string ChangelogUrl { get; }
}