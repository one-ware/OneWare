using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Shared.Helpers
{
    public static class PlatformHelper
    {
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
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Failed open: " + link + " | " + e.Message, e, true, true);
            }
        }

        public static void OpenExplorerPath(string path)
        {
            try
            {
                if (Path.HasExtension(path)) path = Path.GetDirectoryName(path) ?? "";
                else path = Path.GetFullPath(path);
                
                if(string.IsNullOrEmpty(path)) return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error("Can't open " + path + " in explorer. " + e, e, true, true);
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.ProcessArchitecture is not Architecture.Wasm) 
                ExecBash("chmod 777 " + path);
        }

        public static void ChmodFolder(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.ProcessArchitecture is not Architecture.Wasm) 
                ExecBash("chmod -R 777 " + path);
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
    }
}