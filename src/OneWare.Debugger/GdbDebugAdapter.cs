using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class GdbDebugAdapter(ILogger logger, ISettingsService settingsService) : IDebugAdapter
{
    public string Id => "gdb-debug-adapter";
    public string DisplayName => "GDB-Debug-Adapter";
    public string Description => "GNU Debugger Adapter via MI - includes GDB Binary Resolution";
    

    public bool CanLaunch(DebugLaunchRequest launchRequest)
    {
        return ResolveGdbPath() != null &&
               !string.IsNullOrWhiteSpace(launchRequest.ExecutablePath) &&
               File.Exists(launchRequest.ExecutablePath);
    }

    // Liest den GDB-Pfad aus der Einstellung 
    private string? ResolveGdbPath()
    {
        if (!settingsService.HasSetting(DebuggerModule.GdbPathSetting)) return null;

        var configured = settingsService.GetSettingValue<string>(DebuggerModule.GdbPathSetting);
        if (!string.IsNullOrWhiteSpace(configured) && PlatformHelper.Exists(configured))
            return configured;

        return null;
    }
}