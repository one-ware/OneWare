using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

// Das GDB-Backend des Kerns. Deckt lokales Debuggen und, ueber
// RemoteEndpoint, auch angehaengte Ziele wie den SVNR ab.
public class GdbSessionLauncher(ILogger logger, ISettingsService settingsService, IPaths paths) : IDebugSessionLauncher
{
    public const string BackendId = "gdb_server";

    public string Id => BackendId;
    
    public bool CanLaunch(DebugLaunchRequest launchRequest)
    {
        if (ResolveGdbPath() == null)
        {
            logger.Warning("No GDB found. Set the path in the settings under Tools -> Debugger.");
            return false;
        }

        // Ohne Programmdatei startet GDB ohne Ziel. Das ist kein Fehler: die Console beantwortet
        // trotzdem Kommandos, und ein Ziel, das sein Programm selbst haelt, braucht gar keine.
        if (string.IsNullOrWhiteSpace(launchRequest.ExecutablePath)) return true;

        if (File.Exists(launchRequest.ExecutablePath)) return true;

        logger.Warning($"Executable not found: '{launchRequest.ExecutablePath}'");
        return false;
    }

    public IDebugSession CreateSession(DebugLaunchRequest launchRequest)
    {
        var gdbPath = ResolveGdbPath()
                      ?? throw new InvalidOperationException("No GDB executable configured.");

        // mi-async braucht ein GDB, das Kommandos waehrend des Laufs annimmt. Beim lokalen
        // Debuggen ist das unter Windows nicht verlaesslich, dort wird per Signal angehalten.
        //
        // Bei einem angehaengten Ziel gilt das nicht: dort unterbricht GDB ueber ein
        // 0x03-Byte auf der Verbindung zum Stub, nicht ueber ein Signal an einen Prozess.
        // Das ist plattformunabhaengig und trifft immer denselben Empfaenger - anders als
        // das Ctrl+C aus GdbHelper.SendCtrlC, das aus einer Anwendung ohne eigene Konsole
        // heraus nur manchmal ankommt. Nebenbei nimmt die Console damit auch waehrend des
        // Laufs Kommandos an, statt sie mit einer Meldung abzuweisen.
        var asyncMode = !string.IsNullOrWhiteSpace(launchRequest.RemoteEndpoint)
                        || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        return new GdbSession(gdbPath, launchRequest.ExecutablePath, launchRequest.RemoteEndpoint,
            launchRequest.WorkingDirectory, launchRequest.InitCommands, asyncMode, logger);
    }

    // Ermittelt den zu verwendenden GDB-Pfad.
    // Ein explizit gesetztes Setting hat Vorrang; ist es leer, wird bei jedem Aufruf neu gesucht.
    // Dadurch wirkt eine nachträgliche GDB-Installation sofort, ohne dass die Einstellung
    // zurückgesetzt werden muss.
    private string? ResolveGdbPath()
    {
        if (!settingsService.HasSetting(DebuggerModule.GdbPathSetting))
            return GdbLocator.Find(paths.NativeToolsDirectory);

        var configured = settingsService.GetSettingValue<string>(DebuggerModule.GdbPathSetting);

        if (string.IsNullOrWhiteSpace(configured))
            return GdbLocator.Find(paths.NativeToolsDirectory);

        return PlatformHelper.Exists(configured) ? configured : null;
    }
}
