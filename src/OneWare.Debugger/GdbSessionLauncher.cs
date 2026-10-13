using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneWare.Debugger.Helpers;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

// The core's GDB backend. Covers local debugging and, through
// RemoteEndpoint, attached targets as well.
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

        // Without a program file GDB starts without a target. That is not an error: the console
        // still answers commands, and a target that holds its program itself needs none at all.
        if (string.IsNullOrWhiteSpace(launchRequest.ExecutablePath)) return true;

        if (File.Exists(launchRequest.ExecutablePath)) return true;

        logger.Warning($"Executable not found: '{launchRequest.ExecutablePath}'");
        return false;
    }

    public IDebugSession CreateSession(DebugLaunchRequest launchRequest)
    {
        var gdbPath = ResolveGdbPath()
                      ?? throw new InvalidOperationException("No GDB executable configured.");

        // mi-async needs a GDB that accepts commands while running. For local
        // debugging that is not reliable under Windows, where halting goes by signal.
        //
        // That does not apply to an attached target: there GDB interrupts through a
        // 0x03 byte on the connection to the stub, not through a signal to a process.
        // That is platform independent and always reaches the same receiver - unlike
        // the Ctrl+C from GdbHelper.SendCtrlC, which only sometimes arrives when sent from
        // an application without its own console. As a side effect the console then also
        // accepts commands while running, instead of rejecting them with a message.
        var asyncMode = !string.IsNullOrWhiteSpace(launchRequest.RemoteEndpoint)
                        || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        return new GdbSession(gdbPath, launchRequest.ExecutablePath, launchRequest.RemoteEndpoint,
            launchRequest.WorkingDirectory, WithRemoteProtocolLog(launchRequest.InitCommands), asyncMode, logger);
    }

    private IReadOnlyList<string> WithRemoteProtocolLog(IReadOnlyList<string>? initCommands)
    {
        var commands = new List<string>(initCommands ?? []);

        if (settingsService.HasSetting(DebuggerModule.RemoteProtocolLogSetting))
            commands.Add(DebuggerModule.RemoteProtocolLogCommand(
                settingsService.GetSettingValue<bool>(DebuggerModule.RemoteProtocolLogSetting)));

        return commands;
    }

    // Determines the GDB path to use.
    // An explicitly set setting takes precedence; if it is empty, a new search runs on every call.
    // A GDB installed later therefore takes effect immediately, without the setting
    // having to be reset.
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
