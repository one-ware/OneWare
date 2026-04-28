using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public sealed class GdbDebugAdapter : IDebugAdapter
{
    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ISettingsService _settingsService;

    public GdbDebugAdapter(
        ILogger logger,
        ISettingsService settingsService,
        IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
    }

    public string Id => "gdb";
    public string DisplayName => "GDB";
    public string Description => "GNU Debugger via MI";

    public bool CanLaunch(DebugLaunchRequest launchRequest)
    {
        return !string.IsNullOrWhiteSpace(ResolveGdbPath()) &&
               !string.IsNullOrWhiteSpace(launchRequest.ExecutablePath) &&
               File.Exists(launchRequest.ExecutablePath);
    }

    public IDebugSession CreateSession(DebugLaunchRequest launchRequest)
    {
        var gdbPath = ResolveGdbPath();
        if (string.IsNullOrWhiteSpace(gdbPath))
            throw new InvalidOperationException("No GDB executable configured.");

        var asyncMode = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        return new GdbSession(
            gdbPath,
            launchRequest.ExecutablePath,
            asyncMode,
            _logger,
            _projectExplorerService,
            _mainDockService);
    }

    private string? ResolveGdbPath()
    {
        if (_settingsService.HasSetting(DebuggerModule.GdbPathSetting))
        {
            var configured = _settingsService.GetSettingValue<string>(DebuggerModule.GdbPathSetting);
            if (!string.IsNullOrWhiteSpace(configured) &&
                (File.Exists(configured) || PlatformHelper.ExistsOnPath(configured)))
                return configured;
        }

        var defaultName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gdb.exe" : "gdb";
        return PlatformHelper.ExistsOnPath(defaultName) ? defaultName : null;
    }
}
