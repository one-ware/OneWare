using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

/// <summary>
/// Default implementation of <see cref="IDebuggerService"/>. Owns the active
/// <see cref="GdbSession"/>, applies the shared breakpoints to it and forwards
/// GDB stdout/stderr to the IDE output panel.
/// </summary>
public class DebuggerService : IDebuggerService
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IMainDockService _mainDockService;
    private readonly IOutputService _outputService;

    public DebuggerService(ILogger logger,
        ISettingsService settingsService,
        IProjectExplorerService projectExplorerService,
        IMainDockService mainDockService,
        IOutputService outputService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _projectExplorerService = projectExplorerService;
        _mainDockService = mainDockService;
        _outputService = outputService;

        Breakpoints.Breakpoints.CollectionChanged += OnBreakpointsCollectionChanged;
    }

    public BreakpointStore Breakpoints { get; } = BreakpointStore.Instance;

    public GdbSession? CurrentSession { get; private set; }

    public bool IsActive => CurrentSession != null;

    public event EventHandler? StateChanged;

    public async Task<bool> StartAsync(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            _outputService.WriteLine($"[Debugger] Executable not found: {executable}");
            return false;
        }

        // Stop any existing session before starting a new one.
        Stop();

        var gdbPath = ResolveGdbPath();
        if (string.IsNullOrWhiteSpace(gdbPath))
        {
            _outputService.WriteLine(
                "[Debugger] No GDB executable configured. Set 'Tools/Debugger/GDB Path' in settings.");
            return false;
        }

        // Async MI mode is supported by modern gdb on all major desktop OSes,
        // but on Windows we fall back to ctrl-c-based pause via SIGINT helper.
        var asyncMode = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var session = new GdbSession(gdbPath, executable, asyncMode, _logger,
            _projectExplorerService, _mainDockService);

        session.OutputReceived += OnOutputReceived;
        session.CommandRun += OnCommandRun;
        session.Exited += OnSessionExited;
        session.EventFired += OnSessionEvent;

        CurrentSession = session;
        RaiseStateChanged();

        _outputService.WriteLine($"[Debugger] Starting GDB ({gdbPath}) for {executable}");
        var ok = await session.RunAsync(false);
        if (!ok)
        {
            _outputService.WriteLine("[Debugger] Failed to start GDB session.");
            DisposeSession();
            return false;
        }

        // Apply currently set breakpoints.
        foreach (var bp in Breakpoints.Breakpoints.ToArray())
            session.InsertBreakPoint(bp);

        // Run the program. -exec-run starts the inferior; if not supported
        // (e.g. for already-attached scenarios), -exec-continue is fallback.
        var run = session.Run();
        if (run is { Status: CommandStatus.Error })
            session.Continue();

        RaiseStateChanged();
        return true;
    }

    public void Stop()
    {
        var session = CurrentSession;
        if (session == null) return;
        try
        {
            session.Stop();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while stopping debugger session");
        }
        finally
        {
            DisposeSession();
        }
    }

    public void Continue() => CurrentSession?.Continue();
    public void Pause() => CurrentSession?.Pause();
    public void Step() => CurrentSession?.Step();
    public void Next() => CurrentSession?.Next();
    public void Finish() => CurrentSession?.Finish();

    private string? ResolveGdbPath()
    {
        const string settingKey = DebuggerModule.GdbPathSetting;
        if (_settingsService.HasSetting(settingKey))
        {
            var configured = _settingsService.GetSettingValue<string>(settingKey);
            if (!string.IsNullOrWhiteSpace(configured) &&
                (File.Exists(configured) || PlatformHelper.ExistsOnPath(configured)))
                return configured;
        }

        // Fallbacks: try a sensible default for the current OS.
        var defaultName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gdb.exe" : "gdb";
        if (PlatformHelper.ExistsOnPath(defaultName)) return defaultName;
        return null;
    }

    private void OnBreakpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var session = CurrentSession;
        if (session == null) return;
        if (e.NewItems != null)
            foreach (BreakPoint bp in e.NewItems)
                session.InsertBreakPoint(bp);
        if (e.OldItems != null)
            foreach (BreakPoint bp in e.OldItems)
                session.RemoveBreakPoint(bp);
    }

    private void OnOutputReceived(object? sender, string line)
    {
        Dispatcher.UIThread.Post(() => _outputService.WriteLine(line));
    }

    private void OnCommandRun(object? sender, string cmd)
    {
        // Surface the actual MI commands at debug log level only.
        _logger.LogDebug("GDB > {Command}", cmd);
    }

    private void OnSessionEvent(object? sender, GdbEventArgs e)
    {
        // *running / *stopped events affect available actions.
        Dispatcher.UIThread.Post(RaiseStateChanged);
    }

    private void OnSessionExited(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _outputService.WriteLine("[Debugger] GDB session exited.");
            DisposeSession();
        });
    }

    private void DisposeSession()
    {
        var session = CurrentSession;
        if (session != null)
        {
            session.OutputReceived -= OnOutputReceived;
            session.CommandRun -= OnCommandRun;
            session.Exited -= OnSessionExited;
            session.EventFired -= OnSessionEvent;
        }
        CurrentSession = null;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
