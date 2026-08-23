using Mono.Unix.Native;
using OneWare.Essentials.Helpers;

namespace OneWare.Debugger.Helpers;

public class GdbHelper
{
    // Haelt den Debugger-Prozess an, damit GDB waehrend eines laufenden Ziels wieder Kommandos
    // annimmt. Unter Windows gibt es dafuer nichts Brauchbares: Ein Ctrl+C aus einer Anwendung
    // ohne eigene Konsole kommt am fremden Prozess nicht an, und das frueher mitgelieferte
    // SIGINT.exe wurde nie mit ausgeliefert und liess Stop mitten im Aufraeumen scheitern.
    // -> Dort haelt nur das angehaengte Ziel an, ueber -exec-interrupt auf der Verbindung.
    public static int SendCtrlC(int pid)
    {
        return PlatformHelper.Platform switch
        {
            PlatformId.WinX64 or PlatformId.WinArm64 => 0,
            _ => Syscall.kill(pid, Signum.SIGINT)
        };
    }
}
