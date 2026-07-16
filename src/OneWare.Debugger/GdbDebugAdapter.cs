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
 
    public static string SuggestGdbPath()
    {
        // Plattformüblicher Standard-Installationsort zuerst prüfen.
        string? defaultLocation = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            defaultLocation = @"C:\Program Files\gdb\bin\gdb.exe";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            defaultLocation = "/opt/homebrew/bin/gdb";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            defaultLocation = "/usr/bin/gdb";

        if (defaultLocation != null && File.Exists(defaultLocation))
            return defaultLocation;

        // Kein GDB am Standardort -> im System-PATH suchen.
        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gdb.exe" : "gdb";
        return PlatformHelper.GetFullPath(binaryName) ?? binaryName;
    }


    public bool CanLaunch(DebugLaunchRequest launchRequest)
    {
        return ResolveGdbPath() != null &&
               !string.IsNullOrWhiteSpace(launchRequest.ExecutablePath) &&
               File.Exists(launchRequest.ExecutablePath);
    }

    // Liest den GDB-Pfad aus der Einstellung (deren Default beim Start über
    // SuggestGdbPath() vorbelegt wird). null bedeutet: kein nutzbares GDB gefunden.
    private string? ResolveGdbPath()
    {
        if (!settingsService.HasSetting(DebuggerModule.GdbPathSetting)) return null;

        var configured = settingsService.GetSettingValue<string>(DebuggerModule.GdbPathSetting);
        if (!string.IsNullOrWhiteSpace(configured) && PlatformHelper.Exists(configured))
            return configured;

        return null;
    }
}