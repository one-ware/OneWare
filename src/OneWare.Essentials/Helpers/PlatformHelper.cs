using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Essentials.Helpers;

public enum PlatformId
{
    WinX64,
    WinArm64,
    LinuxX64,
    LinuxArm64,
    OsxX64,
    OsxArm64,
    Wasm,
    Unknown
}

public static class PlatformHelper
{
    static PlatformHelper()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Platform = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => PlatformId.WinX64,
                Architecture.Arm64 => PlatformId.WinArm64,
                _ => PlatformId.Unknown
            };
            ExecutableExtension = ".exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Platform = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => PlatformId.LinuxX64,
                Architecture.Arm64 => PlatformId.LinuxArm64,
                _ => PlatformId.Unknown
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Platform = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => PlatformId.OsxX64,
                Architecture.Arm64 => PlatformId.OsxArm64,
                _ => PlatformId.Unknown
            };
        }
        else if (RuntimeInformation.OSArchitecture == Architecture.Wasm)
        {
            Platform = PlatformId.Wasm;
        }
    }

    public static PlatformId Platform { get; }

    public static string PlatformIdentifier => Platform switch
    {
        PlatformId.WinX64 => "win-x64",
        PlatformId.WinArm64 => "win-arm64",
        PlatformId.LinuxX64 => "linux-x64",
        PlatformId.LinuxArm64 => "linux-arm64",
        PlatformId.OsxX64 => "osx-x64",
        PlatformId.OsxArm64 => "osx-arm64",
        PlatformId.Wasm => "wasm",
        _ => "unknown"
    };

    public static string ExecutableExtension { get; } = string.Empty;

    public static string GetLibraryFileName(string libraryName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"{libraryName}.dll"; // Windows uses .dll
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"lib{libraryName}.dylib"; // macOS uses .dylib
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"lib{libraryName}.so"; // Linux uses .so

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }
    
    public static bool Exists(string path)
    {
        return File.Exists(path) || ExistsOnPath(path);
    }

    public static bool ExistsOnPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        return GetFullPath(fileName) != null;
    }

    public static string? GetFullPath(string fileName)
    {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        var values = Environment.GetEnvironmentVariable("PATH");
        if (values == null) return fileName;
        foreach (var path in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    public static void OpenHyperLink(string link)
    {
        try
        {
            var ps = new ProcessStartInfo(link)
            {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }
        catch (Exception e)
        {
            string errorMsg = "Failed open: " + link + " | " + e.Message;
            ContainerLocator.Container.Resolve<ILogger>()?.LogError(e, e.Message);
            UserNotification.NewError(errorMsg)
                .ViaOutput()
                .ViaWindow()
                .Send();
        }
    }

    public static void OpenExplorerPath(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use "explorer /select," to open the folder and select the file
                Process.Start(new ProcessStartInfo("explorer", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"-R \"{path}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try opening the directory if the path is a file
                if (System.IO.File.Exists(path))
                {
                    path = Path.GetDirectoryName(path) ?? throw new NullReferenceException(nameof(path));
                }
                Process.Start("xdg-open", $"\"{path}\"");
            }
            else
            {
                throw new NotSupportedException("Operating system not supported");
            }
        }
        catch (Exception e)
        {
            string errorMsg = "Can't open " + path + " in explorer. ";
            ContainerLocator.Container.Resolve<ILogger>()
                .LogError(e, errorMsg);
            UserNotification.NewError(errorMsg)
                .ViaOutput()
                .ViaWindow()
                .Send();
        }
    }

    #region File Management

    public static void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        File.Copy(sourcePath, destinationPath, overwrite);
        ChmodFile(destinationPath);
    }

    public static void CopyDirectory(string sourcePath, string destPath)
    {
        var dir = new DirectoryInfo(sourcePath);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(destPath);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destPath, file.Name);
            file.CopyTo(targetFilePath);
            ChmodFile(targetFilePath);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destPath, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public static void ExecBash(string cmd)
    {
        var escapedArgs = cmd.Replace("\"", "\\\"");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "/bin/bash",
            Arguments = $"-c \"{escapedArgs}\""
        };

        process.Start();
        process.WaitForExit();
    }

    public static void CreateFile(string path)
    {
        File.Create(path).Close();
        ChmodFile(path);
    }

    public static void WriteTextFile(string path, string text)
    {
        File.WriteAllText(path, text);
        ChmodFile(path);
    }

    public static async Task WriteTextFileAsync(string path, string text)
    {
        await File.WriteAllTextAsync(path, text);
        ChmodFile(path);
    }

    public static void ChmodFile(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            RuntimeInformation.ProcessArchitecture is not Architecture.Wasm)
            ExecBash($"chmod 777 '{path}'");
    }

    public static void ChmodFolder(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            RuntimeInformation.ProcessArchitecture is not Architecture.Wasm)
            ExecBash($"chmod -R 777 '{path}'");
    }

    #endregion

    #region BringWindowToFront WINDOWS

    private const int Alt = 0xA4;
    private const int Extendedkey = 0x1;
    private const int Keyup = 0x2;
    private const int ShowMaximized = 3;

#pragma warning disable IDE1006

    //WINDOWS
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    //LINUX
    [DllImport("libX11.so.6")]
    public static extern int XRaiseWindow(IntPtr display, IntPtr window);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XOpenDisplay(IntPtr display);

#pragma warning disable IDE1006

    public static void ActivateWindow(Process process)
    {
        ActivateWindow(process.MainWindowHandle, process.Handle);
    }

    public static void ActivateWindow(IntPtr mainWindowHandle, IntPtr displayHandle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Guard: check if window already has focus.
            if (mainWindowHandle == GetForegroundWindow()) return;

            // Show window maximized.
            ShowWindow(mainWindowHandle, 1);

            // Simulate an "ALT" key press.
            keybd_event(Alt, 0x45, Extendedkey | 0, 0);

            // Simulate an "ALT" key release.
            keybd_event(Alt, 0x45, Extendedkey | Keyup, 0);

            // Show window in forground.
            SetForegroundWindow(mainWindowHandle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            XRaiseWindow(XOpenDisplay(IntPtr.Zero), mainWindowHandle);
        }
    }

    #endregion

    #region Window Styles

    public static Thickness WindowsOnlyBorder =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            ? new Thickness(1)
            : new
                Thickness(0);

    public static CornerRadius WindowsCornerRadius => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                                                      Application.Current?.ApplicationLifetime is
                                                          IClassicDesktopStyleApplicationLifetime
        ? Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(8) : new CornerRadius(0)
        : new CornerRadius(0);

    public static CornerRadius WindowsCornerRadiusBottom => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                                                            Application.Current?.ApplicationLifetime is
                                                                IClassicDesktopStyleApplicationLifetime
        ? Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(0, 0, 8, 8) : new CornerRadius(0)
        : new CornerRadius(0);

    public static CornerRadius WindowsCornerRadiusBottomLeft =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            ? Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(0, 0, 0, 8) : new CornerRadius(0)
            : new CornerRadius(0);

    public static CornerRadius WindowsCornerRadiusBottomRight =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            ? Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(0, 0, 8, 0) : new CornerRadius(0)
            : new CornerRadius(0);

    #endregion

    #region Keys

    public static KeyModifiers ControlKey => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? KeyModifiers.Meta
        : KeyModifiers.Control;

    public static bool IsControl(KeyEventArgs e)
    {
        switch (Platform)
        {
            case PlatformId.OsxArm64:
            case PlatformId.OsxX64:
                return e.KeyModifiers.HasFlag(ControlKey) || e.Key is Key.LWin or Key.RWin;
            default:
                return e.KeyModifiers.HasFlag(ControlKey) || e.Key is Key.LeftCtrl or Key.RightCtrl;
        }
    }

    #endregion
    
    #region Common FileDialogFilters

    public static readonly FilePickerFileType ExeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? new FilePickerFileType("Executable (*.exe)")
        {
            Patterns = new[] { "*.exe" }
        }
        : new FilePickerFileType("Executable (*)")
        {
            Patterns = new[] { "*" }
        };

    public static readonly FilePickerFileType AllFiles = new("All files (*)")
    {
        Patterns = new[] { "*" }
    };

    #endregion

    private static readonly IPEndPoint DefaultLoopbackEndpoint = new(IPAddress.Loopback, 0);

    public static int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(DefaultLoopbackEndpoint);

        return (socket.LocalEndPoint as IPEndPoint)?.Port ?? throw new Exception("Error getting free port!");
    }
}