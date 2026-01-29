using System.Diagnostics;
using Mono.Unix.Native;
using OneWare.Essentials.Helpers;

namespace OneWare.Debugger.Helpers;

public class GdbHelper
{
    public static int SendCtrlC(int pid)
    {
        switch (PlatformHelper.Platform)
        {
            case PlatformId.WinX64:
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "SIGINT.exe"),
                    Arguments = $"{pid}"
                };

                process.ErrorDataReceived += (o, i) => { Console.WriteLine(i.Data); };
                process.OutputDataReceived += (o, i) => { Console.WriteLine("CTRL+C: " + i.Data); };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit(1000);

                return 1;
            }
            case PlatformId.WinArm64:
                return 0;
            default:
                return Syscall.kill(pid, Signum.SIGINT);
        }
    }
}