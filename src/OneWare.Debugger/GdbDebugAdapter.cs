using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.Debugger;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

/// <summary>
///     Das GDB-Backend des Kerns. Deckt lokales Debuggen und, ueber
///     <see cref="DebugLaunchRequest.RemoteEndpoint" />, auch angehaengte Ziele wie den SVNR ab.
/// </summary>
public class GdbDebugAdapter(ILogger logger, ISettingsService settingsService, IPaths paths) : IDebugAdapter
{
    public const string AdapterId = "gdb";

    public string Id => AdapterId;

    public string DisplayName => "GDB";

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

        // mi-async braucht ein GDB, das Kommandos waehrend des Laufs annimmt. Unter Windows ist
        // das nicht verlaesslich, dort wird stattdessen per Signal angehalten.
        var asyncMode = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        return new GdbSession(gdbPath, launchRequest.ExecutablePath, launchRequest.RemoteEndpoint,
            launchRequest.WorkingDirectory, asyncMode, logger);
    }

    /// <summary>
    ///     Ermittelt den zu verwendenden GDB-Pfad.
    ///     Ein explizit gesetztes Setting hat Vorrang; ist es leer, wird bei jedem Aufruf neu gesucht.
    ///     Dadurch wirkt eine nachträgliche GDB-Installation sofort, ohne dass die Einstellung
    ///     zurückgesetzt werden muss.
    /// </summary>
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
