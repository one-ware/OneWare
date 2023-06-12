using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OneWare.Shared
{
    public enum ShellExecutorType
    {
        Generic,
        Windows,
        Unix
    }

    public static class PlatformSupport //From AvalonStudio
    {
        private static readonly ShellExecutorType ExecutorType;

        static PlatformSupport()
        {
            switch (Platform.PlatformIdentifier)
            {
                case Platform.PlatformId.Win32Nt:
                    ExecutorType = ShellExecutorType.Windows;
                    break;

                case Platform.PlatformId.Unix:
                case Platform.PlatformId.MacOsx:
                    ExecutorType = ShellExecutorType.Unix;
                    break;
            }
        }

        public static void LaunchShell(string workingDirectory, params string[] paths)
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory
            };

            foreach (var extraPath in paths)
                if (extraPath != null)
                    startInfo.Environment["PATH"] += $"{Path.PathSeparator}{extraPath}";

            if (ExecutorType == ShellExecutorType.Windows)
            {
                startInfo.FileName = ResolveFullExecutablePath("cmd.exe");
                startInfo.Arguments = $"/c start {startInfo.FileName}";
            }
            else //Unix
            {
                startInfo.FileName = "sh";
            }

            Process.Start(startInfo);
        }

        public static int ExecuteShellCommand(string commandName, string args, Action<object, DataReceivedEventArgs>
                outputReceivedCallback, Action<object, DataReceivedEventArgs>? errorReceivedCallback = null,
            bool resolveExecutable = true,
            string workingDirectory = "", bool executeInShell = true, bool includeSystemPaths = true,
            params string[] extraPaths)
        {
            using (var shellProc = new Process
                   {
                       StartInfo = new ProcessStartInfo
                       {
                           RedirectStandardOutput = true,
                           RedirectStandardError = true,
                           UseShellExecute = false,
                           WorkingDirectory = workingDirectory
                       }
                   })

            {
                if (!includeSystemPaths) shellProc.StartInfo.Environment["PATH"] = "";
                foreach (var extraPath in extraPaths)
                    if (extraPath != null)
                        shellProc.StartInfo.Environment["PATH"] += $"{Path.PathSeparator}{extraPath}";

                if (executeInShell)
                {
                    if (ExecutorType == ShellExecutorType.Windows)
                    {
                        shellProc.StartInfo.FileName = ResolveFullExecutablePath("cmd.exe");
                        shellProc.StartInfo.Arguments =
                            $"/C {(resolveExecutable ? ResolveFullExecutablePath(commandName, true, extraPaths) : commandName)} {args}";
                        shellProc.StartInfo.CreateNoWindow = true;
                    }
                    else //Unix
                    {
                        shellProc.StartInfo.FileName = "sh";
                        shellProc.StartInfo.Arguments =
                            $"-c \"{(resolveExecutable ? ResolveFullExecutablePath(commandName) : commandName)} {args}\"";
                        shellProc.StartInfo.CreateNoWindow = true;
                    }
                }
                else
                {
                    shellProc.StartInfo.FileName = resolveExecutable
                        ? ResolveFullExecutablePath(commandName, true, extraPaths)
                        : commandName;
                    shellProc.StartInfo.Arguments = args;
                    shellProc.StartInfo.CreateNoWindow = true;
                }

                shellProc.OutputDataReceived += (s, a) => outputReceivedCallback(s, a);

                if (errorReceivedCallback != null) shellProc.ErrorDataReceived += (s, a) => errorReceivedCallback(s, a);

                shellProc.Start();

                shellProc.BeginOutputReadLine();
                shellProc.BeginErrorReadLine();

                shellProc.WaitForExit();

                return shellProc.ExitCode;
            }
        }

        /// <summary>
        ///     Checks whether a script executable is available in the user's shell
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool CheckExecutableAvailability(string fileName, params string[] extraPaths)
        {
            return ResolveFullExecutablePath(fileName, true, extraPaths) != null;
        }

        /// <summary>
        ///     Attempts to locate the full path to a script
        /// </summary>
        public static string? ResolveFullExecutablePath(string fileName, bool returnNullOnFailure = true,
            params string[] extraPaths)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            if (ExecutorType == ShellExecutorType.Windows)
            {
                var values = new List<string>(extraPaths);
                values.AddRange(new List<string>(Environment.GetEnvironmentVariable("PATH")!.Split(';')));

                foreach (var path in values)
                {
                    var fullPath = Path.Combine(path, fileName);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
            else
            {
                //Use the which command
                var outputBuilder = new StringBuilder();
                ExecuteShellCommand("which", $"\"{fileName}\"", (s, e) => { outputBuilder.AppendLine(e.Data); },
                    (s, e) => { }, false);
                var procOutput = outputBuilder.ToString();
                if (string.IsNullOrWhiteSpace(procOutput)) return returnNullOnFailure ? null : fileName;
                return procOutput.Trim();
            }

            return returnNullOnFailure ? null : fileName;
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
    }
}