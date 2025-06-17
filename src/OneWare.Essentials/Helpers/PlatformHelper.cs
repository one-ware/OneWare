using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

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

public class PlatformHelper
{
    private readonly ILogger<PlatformHelper> _logger;

    public PlatformId Platform { get; }
    public string ExecutableExtension { get; } = string.Empty;
    public string PlatformIdentifier => Platform switch
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

    public FilePickerFileType ExeFile { get; }
    public FilePickerFileType AllFiles { get; } = new("All files (*)") { Patterns = new[] { "*" } };

    private static readonly IPEndPoint DefaultLoopbackEndpoint = new(IPAddress.Loopback, 0);

    public PlatformHelper(ILogger<PlatformHelper> logger)
    {
        _logger = logger;

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
        else
        {
            Platform = PlatformId.Unknown;
        }

        ExeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new FilePickerFileType("Executable (*.exe)") { Patterns = new[] { "*.exe" } }
            : new FilePickerFileType("Executable (*)") { Patterns = new[] { "*" } };
    }

    // File helpers
    public bool Exists(string path) => File.Exists(path) || ExistsOnPath(path);

    public bool ExistsOnPath(string fileName) => !string.IsNullOrWhiteSpace(fileName) && GetFullPath(fileName) != null;

    public string? GetFullPath(string fileName)
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

    public void OpenHyperLink(string link)
    {
        try
        {
            var psi = new ProcessStartInfo(link) { UseShellExecute = true, Verb = "open" };
            Process.Start(psi);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to open link: {Link}", link);
        }
    }

    public void OpenExplorerPath(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("explorer", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"-R \"{path}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path)!;

                Process.Start("xdg-open", $"\"{path}\"");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to open path: {Path}", path);
        }
    }

    public string GetLibraryFileName(string libraryName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return $"{libraryName}.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return $"lib{libraryName}.dylib";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return $"lib{libraryName}.so";

        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    // File management
    public void CopyFile(string source, string dest, bool overwrite = false)
    {
        File.Copy(source, dest, overwrite);
        ChmodFile(dest);
    }

    public void CopyDirectory(string source, string dest)
    {
        var dir = new DirectoryInfo(source);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        Directory.CreateDirectory(dest);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(dest, file.Name);
            file.CopyTo(targetFilePath);
            ChmodFile(targetFilePath);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            var newDestinationDir = Path.Combine(dest, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public void CreateFile(string path)
    {
        File.Create(path).Close();
        ChmodFile(path);
    }

    public void WriteTextFile(string path, string text)
    {
        File.WriteAllText(path, text);
        ChmodFile(path);
    }

    public async Task WriteTextFileAsync(string path, string text)
    {
        await File.WriteAllTextAsync(path, text);
        ChmodFile(path);
    }

    public void ChmodFile(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.ProcessArchitecture != Architecture.Wasm)
        {
            ExecBash($"chmod 777 '{path}'");
        }
    }

    public void ChmodFolder(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.ProcessArchitecture != Architecture.Wasm)
        {
            ExecBash($"chmod -R 777 '{path}'");
        }
    }

    public void ExecBash(string cmd)
    {
        var escapedArgs = cmd.Replace("\"", "\\\"");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\""
            }
        };

        process.Start();
        process.WaitForExit();
    }

    public int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(DefaultLoopbackEndpoint);
        return (socket.LocalEndPoint as IPEndPoint)?.Port ?? throw new Exception("Error getting free port!");
    }

    // Windows-only UI Helpers
    public Thickness WindowsOnlyBorder =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            ? new Thickness(1)
            : new Thickness(0);

    public CornerRadius WindowsCornerRadius => GetCornerRadius();
    public CornerRadius WindowsCornerRadiusBottom => GetCornerRadius(bottom: true);
    public CornerRadius WindowsCornerRadiusBottomLeft => GetCornerRadius(bottomLeft: true);
    public CornerRadius WindowsCornerRadiusBottomRight => GetCornerRadius(bottomRight: true);

    private static CornerRadius GetCornerRadius(bool bottom = false, bool bottomLeft = false, bool bottomRight = false)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            return new CornerRadius(0);

        var radius = Environment.OSVersion.Version.Build >= 22000 ? 8 : 0;
        return bottom ? new CornerRadius(0, 0, radius, radius) :
            bottomLeft ? new CornerRadius(0, 0, 0, radius) :
            bottomRight ? new CornerRadius(0, 0, radius, 0) :
            new CornerRadius(radius);
    }

    // Input
    public KeyModifiers ControlKey => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? KeyModifiers.Meta
        : KeyModifiers.Control;

    public bool IsControl(KeyEventArgs e)
    {
        return Platform switch
        {
            PlatformId.OsxArm64 or PlatformId.OsxX64 =>
                e.KeyModifiers.HasFlag(ControlKey) || e.Key is Key.LWin or Key.RWin,
            _ => e.KeyModifiers.HasFlag(ControlKey) || e.Key is Key.LeftCtrl or Key.RightCtrl
        };
    }

    // Window activation
    public void ActivateWindow(Process process)
    {
        ActivateWindow(process.MainWindowHandle, process.Handle);
    }

    public void ActivateWindow(IntPtr mainWindowHandle, IntPtr displayHandle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (mainWindowHandle == GetForegroundWindow()) return;

            ShowWindow(mainWindowHandle, 1);
            keybd_event(Alt, 0x45, Extendedkey | 0, 0);
            keybd_event(Alt, 0x45, Extendedkey | Keyup, 0);
            SetForegroundWindow(mainWindowHandle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            XRaiseWindow(XOpenDisplay(IntPtr.Zero), mainWindowHandle);
        }
    }

    // Win32 Interop
    private const int Alt = 0xA4;
    private const int Extendedkey = 0x1;
    private const int Keyup = 0x2;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("libX11.so.6")] public static extern int XRaiseWindow(IntPtr display, IntPtr window);
    [DllImport("libX11.so.6")] public static extern IntPtr XOpenDisplay(IntPtr display);
}
