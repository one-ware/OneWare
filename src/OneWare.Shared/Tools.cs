using System.Diagnostics;
using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using Prism.Ioc;

namespace OneWare.Shared
{
    public class Tools
    {
        /// <summary>
        ///     Asks to save all files and returns true if ready to close or false if operation was canceled
        /// </summary>
        public static async Task<bool> HandleUnsavedFilesAsync(List<IExtendedDocument> unsavedFiles, Window dialogOwner)
        {
            if (unsavedFiles.Count > 0)
            {
                var status = await ContainerLocator.Container.Resolve<IWindowService>().ShowYesNoCancelAsync("Warning", "Keep unsaved changes?", MessageBoxIcon.Warning);
                
                if (status == MessageBoxStatus.Yes)
                {
                    for (var i = 0; i < unsavedFiles.Count; i++)
                        if (await unsavedFiles[i].SaveAsync())
                        {
                            unsavedFiles.RemoveAt(i);
                            i--;
                        }

                    if (unsavedFiles.Count == 0) return true;

                    var message = "Critical error saving some files: \n";
                    foreach (var file in unsavedFiles) message += file.Title + ", ";
                    message = message.Remove(message.Length - 2);

                    var status2 = await ContainerLocator.Container.Resolve<IWindowService>()
                        .ShowYesNoCancelAsync("Error", $"{message}\nQuit anyways?", MessageBoxIcon.Error);

                    if (status2 == MessageBoxStatus.Yes) return true;
                }
                else if (status == MessageBoxStatus.No)
                {
                    //Quit and discard changes
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
        
        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
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

        public static void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
        {
            File.Copy(sourcePath, destinationPath, overwrite);
            ChmodFile(destinationPath);
        }

        public static void CopyDirectory(string sourcePath, string destPath)
        {
            if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                var dest = Path.Combine(destPath, Path.GetFileName(file));
                File.Copy(file, dest);
            }

            foreach (var folder in Directory.GetDirectories(sourcePath))
            {
                var dest = Path.Combine(destPath, Path.GetFileName(folder));
                CopyDirectory(folder, dest);
            }
        }

        public static T? VisualUpwardSearch<T>(Interactive? source)
        {
            while (source != null && !(source is T)) source = source.GetInteractiveParent();
            if (source == null) return default;
            return (T) Convert.ChangeType(source, typeof(T));
        }

        public static string? GetQuartusTool(string quartusPath, string toolName)
        {
            var bin64Path = Path.Combine(Path.Combine(quartusPath, "bin64", $"{toolName}{Platform.ExecutableExtension}"));
            if (File.Exists(bin64Path)) return bin64Path;
            var bin32Path = Path.Combine(Path.Combine(quartusPath, "bin", $"{toolName}{Platform.ExecutableExtension}"));
            return File.Exists(bin32Path) ? bin32Path : null;
        }
        
        public static string? GetModelSimTool(string modelSimPath, string toolName)
        {
            var path = Path.Combine(modelSimPath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win32aloem" : "bin", $"{toolName}{Platform.ExecutableExtension}");
            return File.Exists(path) ? path : null;
        }

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

        private static string RepeatChar(int number, char c)
        {
            var returnString = "";
            for (var j = 0; j < number; j++) returnString += c;
            return returnString;
        }

        public static bool IsValidFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var chars = Path.GetInvalidFileNameChars();
            foreach (var character in chars)
                if (name.Contains(character))
                    return false;
            return true;
        }

        public static bool IsFontInstalled(string fontName)
        {
            return FontManager.Current.SystemFonts.Contains(fontName);
        }

        public static bool IsValidDirectory(string path)
        {
            return Directory.Exists(path);
        }

        public static void ExecBash(string cmd)
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

        public static string GetNumbers(string input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
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

        private static readonly IPEndPoint DefaultLoopbackEndpoint = new(IPAddress.Loopback, 0);

        public static int GetAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(DefaultLoopbackEndpoint);
                return ((IPEndPoint)socket!.LocalEndPoint!).Port;
            }
        }

        public static string? FirstFileInPath(string path, string extension)
        {
            try
            {
                return Directory
                    .GetFiles(path)
                    .FirstOrDefault(x => Path.GetExtension(x).Equals(extension, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return null;
            }
        }
        
        #region LinuxPermissionMadness

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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Application.Current?.ApplicationLifetime is not ISingleViewApplicationLifetime) ExecBash("chmod 777 " + path);
        }

        public static void ChmodFolder(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ExecBash("chmod -R 777 " + path);
        }

        #endregion
        
        #region FileManager
        
        #nullable enable

        public static async Task<string?> SelectFolderAsync(Window owner, string title, string? startDir)
        {
            var startUpLocation = startDir == null ? null : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
            var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = title,
                SuggestedStartLocation = startUpLocation,
                AllowMultiple = false
            });

            if (result.Count != 1) return null;
            return result[0].TryGetLocalPath();
        }
        
        public static async Task<IEnumerable<string>> SelectFoldersAsync(Window owner, string title, string? startDir)
        {
            var folders = new List<string>();
            
            var startUpLocation = startDir == null ? null : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
            var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = title,
                SuggestedStartLocation = startUpLocation,
                AllowMultiple = true
            });

            foreach (var r in result)
            {
                if (r.TryGetLocalPath() is { } t)
                {
                    folders.Add(t);
                }
            }

            return folders;
        }
        
        public static async Task<string?> SelectSaveFileAsync(TopLevel owner, string title, string? startDir, string defaultExtension, params FilePickerFileType[] filters)
        {
            var startUpLocation = startDir == null ? null : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
            var result = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = title,
                SuggestedStartLocation = startUpLocation,
                FileTypeChoices = filters,
                DefaultExtension = defaultExtension,
            });

            if (result == null) return null;
            return result.TryGetLocalPath();
        }
        
        public static async Task<string?> SelectFileAsync(TopLevel owner, string title, string? startDir, params FilePickerFileType[]? filters)
        {
            var startUpLocation = startDir == null ? null : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
            var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = title,
                SuggestedStartLocation = startUpLocation,
                AllowMultiple = false,
                FileTypeFilter = filters
            });

            if (result.Count != 1) return null;
            return result[0].TryGetLocalPath();
        }
        
        public static async Task<List<string>> SelectFilesAsync(TopLevel owner, string title, string? startDir, params FilePickerFileType[] filters)
        {
            var files = new List<string>();
            var startUpLocation = startDir == null ? null : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
            var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = title,
                SuggestedStartLocation = startUpLocation,
                AllowMultiple = true,
                FileTypeFilter = filters,
            });

            foreach (var r in result)
            {
                if (r.TryGetLocalPath() is {} t)
                {
                    files.Add(t);
                }
            }

            return files;
        }
        #endregion
    }
}