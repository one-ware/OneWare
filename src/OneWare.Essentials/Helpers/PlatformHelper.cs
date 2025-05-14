using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using OneWare.Essentials.Services;
using Autofac;
using System.IO;
using System.Threading.Tasks;

namespace OneWare.Essentials.Helpers
{
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
        private readonly ILogger _logger;

        public PlatformHelper(ILogger logger)
        {
            _logger = logger;
        }

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

        public void OpenHyperLink(string link)
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
                _logger?.Error("Failed open: " + link + " | " + e.Message, e, true, true);
            }
        }

        public void OpenExplorerPath(string path)
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
                _logger?.Error("Can't open " + path + " in explorer. " + e, e, true, true);
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

                // Bring to front
                SetForegroundWindow(mainWindowHandle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Raise window on Linux (X11).
                var display = XOpenDisplay(IntPtr.Zero);
                XRaiseWindow(display, displayHandle);
            }
        }

        #endregion
    }
}
