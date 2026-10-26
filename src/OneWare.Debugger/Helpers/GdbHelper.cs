using Mono.Unix.Native;
using OneWare.Essentials.Helpers;

namespace OneWare.Debugger.Helpers;

public class GdbHelper
{
    // Halts the debugger process so that GDB accepts commands again while a target is
    // running. Under Windows there is nothing usable for that: a Ctrl+C from an application
    // without its own console does not reach the other process, and the SIGINT.exe formerly
    // referenced was never shipped and made Stop fail in the middle of cleanup.
    // -> There only the attached target halts, through -exec-interrupt on the connection.
    public static int SendCtrlC(int pid)
    {
        return PlatformHelper.Platform switch
        {
            PlatformId.WinX64 or PlatformId.WinArm64 => 0,
            _ => Syscall.kill(pid, Signum.SIGINT)
        };
    }
}
