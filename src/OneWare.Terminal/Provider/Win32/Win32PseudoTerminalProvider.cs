using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using static winpty.WinPty;

namespace OneWare.Terminal.Provider.Win32
{
    public class Win32PseudoTerminalProvider : IPseudoTerminalProvider
    {
        public IPseudoTerminal? Create(int columns, int rows, string initialDirectory, string? environment, string? command, params string[]? arguments)
        {
            var cfg = winpty_config_new(WINPTY_FLAG_COLOR_ESCAPES, out var err);
            winpty_config_set_initial_size(cfg, columns, rows);

            var handle = winpty_open(cfg, out err);

            if (err != IntPtr.Zero)
            {
                Console.WriteLine(winpty_error_code(err));
                return null;
            }

            var args = arguments != null ? string.Join(" ", arguments) : "";

            var spawnCfg = winpty_spawn_config_new(WINPTY_SPAWN_FLAG_AUTO_SHUTDOWN, command, args, initialDirectory, environment, out err);
            if (err != IntPtr.Zero)
            {
                Console.WriteLine(winpty_error_code(err));
                return null;
            }

            var stdin = CreatePipe(winpty_conin_name(handle), PipeDirection.Out);
            var stdout = CreatePipe(winpty_conout_name(handle), PipeDirection.In);

            if (!winpty_spawn(handle, spawnCfg, out var process, out var thread, out var procError, out err))
            {
                Console.WriteLine(winpty_error_code(err));
                return null;
            }

            var id = GetProcessId(process);

            var terminalProcess = Process.GetProcessById(id);                      

            return new Win32PseudoTerminal(terminalProcess, handle, cfg, spawnCfg, err, stdin, stdout);
        }

        [DllImport("kernel32.dll")]
        private static extern int GetProcessId(IntPtr handle);

        private static Stream CreatePipe(string pipeName, PipeDirection direction)
        {
            string serverName = ".";

            if (pipeName.StartsWith("\\"))
            {
                int slash3 = pipeName.IndexOf('\\', 2);

                if (slash3 != -1)
                {
                    serverName = pipeName.Substring(2, slash3 - 2);
                }

                int slash4 = pipeName.IndexOf('\\', slash3 + 1);

                if (slash4 != -1)
                {
                    pipeName = pipeName.Substring(slash4 + 1);
                }
            }

            var pipe = new NamedPipeClientStream(serverName, pipeName, direction);

            pipe.Connect();

            return pipe;
        }
    }
}
