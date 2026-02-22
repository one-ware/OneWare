namespace OneWare.Essentials.Services;

public interface IPaths
{
    /// <summary>
    /// Application display name.
    /// </summary>
    public string AppName { get; }
    /// <summary>
    /// Path to the application icon.
    /// </summary>
    public string AppIconPath { get; }
    /// <summary>
    /// Folder name used under app data.
    /// </summary>
    public string AppFolderName { get; }
    /// <summary>
    /// App data base directory.
    /// </summary>
    public string AppDataDirectory { get; }
    /// <summary>
    /// Temporary working directory.
    /// </summary>
    public string TempDirectory { get; }
    /// <summary>
    /// Session directory for transient files.
    /// </summary>
    public string SessionDirectory { get; }
    /// <summary>
    /// Directory for persisted layouts.
    /// </summary>
    public string LayoutDirectory { get; }
    /// <summary>
    /// Path to the main settings file.
    /// </summary>
    public string SettingsPath { get; }
    /// <summary>
    /// Documents base directory.
    /// </summary>
    public string DocumentsDirectory { get; }
    /// <summary>
    /// Crash report directory.
    /// </summary>
    public string CrashReportsDirectory { get; }
    /// <summary>
    /// Default projects directory.
    /// </summary>
    public string ProjectsDirectory { get; }
    /// <summary>
    /// Package installation directory.
    /// </summary>
    public string PackagesDirectory { get; }
    /// <summary>
    /// Native tools directory.
    /// </summary>
    public string NativeToolsDirectory { get; }
    /// <summary>
    /// ONNX runtime package directory.
    /// </summary>
    public string OnnxRuntimesDirectory { get; }
    /// <summary>
    /// Plugins directory.
    /// </summary>
    public string PluginsDirectory { get; }
    /// <summary>
    /// URL for the changelog.
    /// </summary>
    public string ChangelogUrl { get; }
    /// <summary>
    /// URL for update info.
    /// </summary>
    public string UpdateInfoUrl { get; }
}
