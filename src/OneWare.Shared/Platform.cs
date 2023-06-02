using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Shared
{
    public static class Platform
    {
        public delegate bool ConsoleCtrlDelegate(CtrlTypes ctrlType);

        public enum CtrlTypes : uint
        {
            CtrlCEvent = 0,
            CtrlBreakEvent,
            CtrlCloseEvent,
            CtrlLogoffEvent = 5,
            CtrlShutdownEvent
        }

        public enum PlatformId
        {
            Win32S = 0,
            Win32Windows = 1,
            Win32Nt = 2,
            WinCe = 3,
            Unix = 4,
            Xbox = 5,
            MacOsx = 6
        }
        
        public enum Signum
        {
            Sighup = 1, // Hangup (POSIX).
            Sigint = 2, // Interrupt (ANSI).
            Sigquit = 3, // Quit (POSIX).
            Sigill = 4, // Illegal instruction (ANSI).
            Sigtrap = 5, // Trace trap (POSIX).
            Sigabrt = 6, // Abort (ANSI).
            Sigiot = 6, // IOT trap (4.2 BSD).
            Sigbus = 7, // BUS error (4.2 BSD).
            Sigfpe = 8, // Floating-point exception (ANSI).
            Sigkill = 9, // Kill, unblockable (POSIX).
            Sigusr1 = 10, // User-defined signal 1 (POSIX).
            Sigsegv = 11, // Segmentation violation (ANSI).
            Sigusr2 = 12, // User-defined signal 2 (POSIX).
            Sigpipe = 13, // Broken pipe (POSIX).
            Sigalrm = 14, // Alarm clock (POSIX).
            Sigterm = 15, // Termination (ANSI).
            Sigstkflt = 16, // Stack fault.
            Sigcld = Sigchld, // Same as SIGCHLD (System V).
            Sigchld = 17, // Child status has changed (POSIX).
            Sigcont = 18, // Continue (POSIX).
            Sigstop = 19, // Stop, unblockable (POSIX).
            Sigtstp = 20, // Keyboard stop (POSIX).
            Sigttin = 21, // Background read from tty (POSIX).
            Sigttou = 22, // Background write to tty (POSIX).
            Sigurg = 23, // Urgent condition on socket (4.2 BSD).
            Sigxcpu = 24, // CPU limit exceeded (4.2 BSD).
            Sigxfsz = 25, // File size limit exceeded (4.2 BSD).
            Sigvtalrm = 26, // Virtual alarm clock (4.2 BSD).
            Sigprof = 27, // Profiling alarm clock (4.2 BSD).
            Sigwinch = 28, // Window size change (4.3 BSD, Sun).
            Sigpoll = Sigio, // Pollable event occurred (System V).
            Sigio = 29, // I/O now possible (4.2 BSD).
            Sigpwr = 30, // Power failure restart (System V).
            Sigsys = 31, // Bad system call.
            Sigunused = 31
        }

        internal const string Libc = "libc";
        private const string Lib = "MonoPosixHelper";

        private const string UserDataDir = ".as";

        internal const uint SymbolicLinkFlagAllowUnprivilegedCreate = 2;

        private static readonly string NumberPattern = " ({0})";

        public static IDictionary EnvironmentVariables => Environment.GetEnvironmentVariables();

        public static string DllExtension
        {
            get
            {
                switch (PlatformIdentifier)
                {
                    case PlatformId.Unix:
                        return ".so";

                    case PlatformId.MacOsx:
                        return ".dylib";

                    case PlatformId.Win32Nt:
                        return ".dll";

                    default:
                        throw new NotImplementedException("Not implemented for your platform.");
                }
            }
        }
        public static string ExecutableExtension
        {
            get
            {
                switch (PlatformIdentifier)
                {
                    case PlatformId.Unix:
                    case PlatformId.MacOsx:
                    {
                        return string.Empty;
                    }

                    case PlatformId.Win32Nt:
                    {
                        return ".exe";
                    }

                    default:
                        throw new NotImplementedException("Not implemented for your platform.");
                }
            }
        }

        public static char DirectorySeperator => Path.DirectorySeparatorChar;

        public static Architecture OsArchitecture => RuntimeInformation.OSArchitecture;
        public static string OsDescription => RuntimeInformation.OSDescription;

        public static PlatformId PlatformIdentifier
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return PlatformId.Win32Nt;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return PlatformId.Unix;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformId.MacOsx;

                throw new Exception("Unknow platform");
            }
        }

        [DllImport("libc")]
        private static extern void chmod(string file, int mode);

        public static void Chmod(string file, int mode)
        {
            if (PlatformIdentifier != PlatformId.Win32Nt) chmod(file, mode);
        }

        public static string NextAvailableDirectoryName(string path)
        {
            // Short-cut if already available
            if (!Directory.Exists(path))
                return path;

            // Otherwise just append the pattern to the path and return next filename
            return GetNextDirectoryName(path + NumberPattern);
        }

        public static string NextAvailableFileName(string path)
        {
            // Short-cut if already available
            if (!File.Exists(path))
                return path;

            // If path has extension then insert the number pattern just before the extension and return next filename
            if (Path.HasExtension(path))
                return GetNextFileName(path.Insert(path.LastIndexOf(Path.GetExtension(path)), NumberPattern));

            // Otherwise just append the pattern to the path and return next filename
            return GetNextFileName(path + NumberPattern);
        }

        private static string GetNextDirectoryName(string pattern)
        {
            var tmp = string.Format(pattern, 1);
            if (tmp == pattern)
                throw new ArgumentException("The pattern must include an index place-holder", "pattern");

            if (!Directory.Exists(tmp))
                return tmp; // short-circuit if no matches

            int min = 1, max = 2; // min is inclusive, max is exclusive/untested

            while (Directory.Exists(string.Format(pattern, max)))
            {
                min = max;
                max *= 2;
            }

            while (max != min + 1)
            {
                var pivot = (max + min) / 2;
                if (Directory.Exists(string.Format(pattern, pivot)))
                    min = pivot;
                else
                    max = pivot;
            }

            return string.Format(pattern, max);
        }

        private static string GetNextFileName(string pattern)
        {
            var tmp = string.Format(pattern, 1);
            if (tmp == pattern)
                throw new ArgumentException("The pattern must include an index place-holder", "pattern");

            if (!File.Exists(tmp))
                return tmp; // short-circuit if no matches

            int min = 1, max = 2; // min is inclusive, max is exclusive/untested

            while (File.Exists(string.Format(pattern, max)))
            {
                min = max;
                max *= 2;
            }

            while (max != min + 1)
            {
                var pivot = (max + min) / 2;
                if (File.Exists(string.Format(pattern, pivot)))
                    min = pivot;
                else
                    max = pivot;
            }

            return string.Format(pattern, max);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern byte
            CreateSymbolicLinkW(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern byte CreateHardLinkW(string lpFileName, string lpExistingFileName,
            IntPtr lpSecurityAttributes);

        public static bool CreateSymbolicLinkWin32(string linkName, string target, bool isFile)
        {
            return CreateSymbolicLinkW(linkName, target, SymbolicLinkFlagAllowUnprivilegedCreate) != 0;
        }

        public static bool CreateHardLinkWin32(string linkName, string target, bool isFile)
        {
            return CreateHardLinkW(linkName, target, IntPtr.Zero) != 0;
        }
        
        public static void OpenWebPage(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }

        public static void OpenFolderInExplorer(string path)
        {
            if (Directory.Exists(path))
                switch (PlatformIdentifier)
                {
                    case PlatformId.Win32Nt:
                        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{path}\"" });
                        break;

                    case PlatformId.Unix:
                        Process.Start(new ProcessStartInfo
                            { FileName = "xdg-open", Arguments = path, CreateNoWindow = true });
                        break;
                }
        }

        // private static int Kill(int pid, Signum sig)
        // {
        //     if (sig == Signum.Sigint) return Syscall.kill(pid, Mono.Unix.Native.Signum.SIGINT);
        //     //TODO add more if needed
        //     return 0;
        // }
        //
        // public static int SendSignal(int pid, Signum sig)
        // {
        //     switch (PlatformIdentifier)
        //     {
        //         case PlatformId.Unix:
        //         case PlatformId.MacOsx:
        //             return Kill(pid, sig);
        //
        //         case PlatformId.Win32Nt:
        //             switch (sig)
        //             {
        //                 case Signum.Sigint:
        //                     return SendCtrlC(pid);
        //
        //                 default:
        //                     throw new NotImplementedException();
        //             }
        //
        //         default:
        //             throw new NotImplementedException();
        //     }
        // }

        public static bool AttachConsole(int pid)
        {
            switch (PlatformIdentifier)
            {
                case PlatformId.Win32Nt:
                    return Win32AttachConsole(pid);

                default:
                    return true;
            }
        }

        public static bool FreeConsole()
        {
            switch (PlatformIdentifier)
            {
                case PlatformId.Win32Nt:
                    return Win32FreeConsole();

                default:
                    return true;
            }
        }

        public static bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handlerRoutine, bool add)
        {
            switch (PlatformIdentifier)
            {
                case PlatformId.Win32Nt:
                    return Win32SetConsoleCtrlHandler(handlerRoutine, add);

                default:
                    return true;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "AttachConsole")]
        private static extern bool Win32AttachConsole(int processId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, EntryPoint = "FreeConsole")]
        private static extern bool Win32FreeConsole();


        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlTypes ctrlEvent, uint processGroupId);

        [DllImport("kernel32.dll", EntryPoint = "SetConsoleCtrlHandler")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Win32SetConsoleCtrlHandler(ConsoleCtrlDelegate handlerRoutine, bool add);

        private static int SendCtrlC(int pid)
        {
            //return GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0) ? 1 : 0;
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "NIOS", "SIGINT.exe"),
                    Arguments = $"{pid}"
                }
            };

            process.ErrorDataReceived += (o, i) => { ContainerLocator.Container.Resolve<ILogger>()?.Error(i.Data); };
            process.OutputDataReceived += (o, i) =>
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Log("CTRL+C: " +  i.Data);
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit(1000);

            return 1;
        }

        public static string ToAvalonPath(this string path)
        {
            return path.Replace('\\', '/');
        }

        public static string NormalizePath(this string path)
        {
            var result = path?.Replace("\\\\", "\\").ToPlatformPath();

            if (!string.IsNullOrEmpty(result))
            {
                var info = new DirectoryInfo(result);

                result = info.FullName;
            }

            return result;
        }

        public static string ToPlatformPath(this string path)
        {
            switch (PlatformIdentifier)
            {
                case PlatformId.Win32Nt:
                    return path.Replace('/', '\\').Trim();
                default:
                    return path.ToAvalonPath().Trim();
            }
        }

        public static bool IsSamePathAs(this string path, string other)
        {
            return path.CompareFilePath(other) == 0;
        }

        public static int CompareFilePath(this string path, string other)
        {
            if (other != null && path != null)
            {
                path = path.NormalizePath().ToAvalonPath();
                other = other.NormalizePath().ToAvalonPath();

                if (other.EndsWith("/") && !path.EndsWith("/"))
                    path += "/";
                else if (path.EndsWith("/") && !other.EndsWith("/")) other += "/";
            }

            if (path == null && other == null) return 0;
            if (path == null) return 1;
            if (other == null) return -1;

            switch (PlatformIdentifier)
            {
                case PlatformId.Win32Nt:
                    // TODO consider using directory info?
                    return path.ToLower().CompareTo(other.ToLower());

                default:

                    return path.CompareTo(other);
            }
        }
    }
}