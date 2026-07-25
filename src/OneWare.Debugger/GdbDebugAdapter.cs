
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class GdbDebugAdapter(ILogger logger, ISettingsService settingsService, IPaths paths) : IDebugAdapter
{
    public string Id => "gdb-debug-adapter";
    public string DisplayName => "GDB-Debug-Adapter";
    public string Description => "GNU Debugger Adapter via MI - includes GDB Binary Resolution";


    public bool CanLaunch(DebugLaunchRequest launchRequest)
    {
        if (ResolveGdbPath() == null)
        {
            logger.Warning("No GDB found. Set the path in the settings under Tools -> Debugger.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(launchRequest.ExecutablePath) &&
            File.Exists(launchRequest.ExecutablePath)) return true;
        
        logger.Warning($"Executable not found: '{launchRequest.ExecutablePath}'");
        return false;

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