using System.Collections.Specialized;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class DebuggerService : IDebuggerService
{
    private readonly ILogger _logger;
    private readonly IOutputService _outputService;

    public DebuggerService(
        ILogger logger,
        IEnumerable<IDebugAdapter> adapters,
        IOutputService outputService)
    {
        _logger = logger;
        _outputService = outputService;
        Adapters = adapters.OrderBy(x => x.DisplayName).ToArray();

        Breakpoints.Breakpoints.CollectionChanged += OnBreakpointsCollectionChanged;
    }

    public BreakpointStore Breakpoints { get; } = BreakpointStore.Instance;

    public IReadOnlyList<IDebugAdapter> Adapters { get; }

    public IDebugSession? CurrentSession { get; private set; }

    public DebugSessionState CurrentState { get; private set; } = DebugSessionState.Empty;

    public bool IsActive => CurrentSession != null;

    public event EventHandler? StateChanged;

    public async Task<bool> StartAsync(DebugLaunchRequest launchRequest)
    {
        if (string.IsNullOrWhiteSpace(launchRequest.ExecutablePath) || !File.Exists(launchRequest.ExecutablePath))
        {
            _outputService.WriteLine($"[Debugger] Executable not found: {launchRequest.ExecutablePath}");
            return false;
        }

        var adapter = Adapters.FirstOrDefault(x => string.Equals(x.Id, launchRequest.AdapterId, StringComparison.OrdinalIgnoreCase));
        if (adapter == null)
        {
            _outputService.WriteLine($"[Debugger] Unknown debugger adapter: {launchRequest.AdapterId}");
            return false;
        }

        if (!adapter.CanLaunch(launchRequest))
        {
            _outputService.WriteLine($"[Debugger] {adapter.DisplayName} is not ready to launch {launchRequest.ExecutablePath}");
            return false;
        }

        Stop();

        IDebugSession session;
        try
        {
            session = adapter.CreateSession(launchRequest);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            _outputService.WriteLine($"[Debugger] Failed to create debugger session: {e.Message}");
            return false;
        }

        session.OutputReceived += OnOutputReceived;
        session.CommandRun += OnCommandRun;
        session.Exited += OnSessionExited;
        session.StateChanged += OnSessionStateChanged;

        CurrentSession = session;
        CurrentState = DebugSessionState.Empty;
        RaiseStateChanged();

        _outputService.WriteLine($"[Debugger] Starting {adapter.DisplayName} for {launchRequest.ExecutablePath}");
        var ok = await session.StartAsync();
        if (!ok)
        {
            _outputService.WriteLine("[Debugger] Failed to start debugger session.");
            DisposeSession();
            return false;
        }

        foreach (var breakpoint in Breakpoints.Breakpoints.ToArray())
            session.InsertBreakpoint(breakpoint);

        session.StartExecution();

        return true;
    }

    public void Stop()
    {
        var session = CurrentSession;
        if (session == null)
            return;

        try
        {
            session.Stop();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        finally
        {
            DisposeSession();
        }
    }

    public void Continue() => CurrentSession?.Continue();
    public void Pause() => CurrentSession?.Pause();
    public void StepInto() => CurrentSession?.StepInto();
    public void StepOver() => CurrentSession?.StepOver();
    public void StepOut() => CurrentSession?.StepOut();

    public async Task ExecuteRawCommandAsync(string command)
    {
        if (CurrentSession == null || string.IsNullOrWhiteSpace(command))
            return;

        await CurrentSession.ExecuteRawCommandAsync(command);
    }

    private void OnBreakpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var session = CurrentSession;
        if (session == null)
            return;

        if (e.NewItems != null)
            foreach (BreakPoint breakpoint in e.NewItems)
                session.InsertBreakpoint(breakpoint);

        if (e.OldItems != null)
            foreach (BreakPoint breakpoint in e.OldItems)
                session.RemoveBreakpoint(breakpoint);
    }

    private void OnOutputReceived(object? sender, string line)
    {
        Dispatcher.UIThread.Post(() => _outputService.WriteLine(line));
    }

    private void OnCommandRun(object? sender, string command)
    {
        _logger.LogDebug("Debugger > {Command}", command);
    }

    private void OnSessionStateChanged(object? sender, DebugSessionStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentState = e.State;
            Breakpoints.CurrentBreakPoint = CurrentState.IsRunning
                ? null
                : CurrentState.CurrentFrame is { FullPath: { Length: > 0 }, Line: > 0 } frame
                    ? Breakpoints.Breakpoints.FirstOrDefault(x => string.Equals(x.File, frame.FullPath, StringComparison.OrdinalIgnoreCase) && x.Line == frame.Line)
                        ?? new BreakPoint { File = frame.FullPath, Line = frame.Line }
                    : null;
            RaiseStateChanged();
        });
    }

    private void OnSessionExited(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _outputService.WriteLine("[Debugger] Debugger session exited.");
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
            session.StateChanged -= OnSessionStateChanged;
        }

        CurrentSession = null;
        CurrentState = DebugSessionState.Empty;
        Breakpoints.CurrentBreakPoint = null;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
