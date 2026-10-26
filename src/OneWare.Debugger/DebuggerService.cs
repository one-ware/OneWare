using OneWare.Debugger.Helpers;
using OneWare.Essentials.Helpers;
using System.Collections.Specialized;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

// Holds the registered backends and owns the one session that can run at a time.
// Everything that leaves this class is marshalled to the UI thread. The session reports
// from its reader thread; collecting that in exactly one place is simpler than asking
// every panel to do it.
public class DebuggerService(ICompositeServiceProvider serviceProvider, ILogger logger) : IDebuggerService
{
    private readonly List<IDebugSessionLauncher> _sessionLaunchers = [];
    private readonly HashSet<BreakPoint> _armed = [];
    private readonly HashSet<BreakPoint> _refused = []; // breakpoints the stub refuses
    private readonly BreakpointStore _breakpointStore = BreakpointStore.Instance;
    private readonly List<IDebugTargetPreparer> _targetPreparers = [];

    private IDebugTargetPreparer? _activePreparer;

    private HashSet<string>? _sourceFiles; // normalized paths, as reported and with links resolved

    // Root folder of the project the session was started for. Only its breakpoints are armed;
    // null (no active project) arms all of them.
    private string? _projectRoot;

    private IDebugSession? _session;

    public IReadOnlyList<IDebugSessionLauncher> SessionLaunchers => _sessionLaunchers;

    public IReadOnlyList<IDebugTargetPreparer> TargetPreparers => _targetPreparers;

    public IDebugSession? CurrentSession => _session;

    public DebugSessionState State { get; private set; } = DebugSessionState.Empty;

    public bool IsActive => _session != null;

    // Arrives with the launch request and holds for the duration of the session. Without a session
    // this is the byte-addressed default profile -> the Memory tab then calculates with 1 as before.
    public DebugTargetProfile TargetProfile { get; private set; } = DebugTargetProfile.Default;

    public event EventHandler? StateChanged;

    public void RegisterSessionLauncher<T>() where T : IDebugSessionLauncher
    {
        var launcher = serviceProvider.Resolve<T>();

        if (_sessionLaunchers.Any(x => x.Id == launcher.Id))
        {
            logger.Warning($"A session launcher with id '{launcher.Id}' is already registered - ignoring.");
            return;
        }

        _sessionLaunchers.Add(launcher);
    }

    public void RegisterTargetPreparer<T>() where T : IDebugTargetPreparer
    {
        var preparer = serviceProvider.Resolve<T>();

        if (_targetPreparers.Any(x => x.GetType() == preparer.GetType()))
        {
            logger.Warning($"A target preparer of type '{preparer.GetType().Name}' is already registered - ignoring.");
            return;
        }

        _targetPreparers.Add(preparer);
    }

    public async Task<bool> StartAsync(DebugLaunchRequest launchRequest)
    {
        await StopAsync();

        return await StartCoreAsync(launchRequest);
    }

    public async Task<bool> StartAsync(IDebugTargetPreparer preparer)
    {
        // Tear down the running session first, then prepare: the preparer uses the same
        // resources as the previous session - a serial port, for instance, cannot be
        // opened twice.
        await StopAsync();

        DebugLaunchRequest? launchRequest;
        try
        {
            launchRequest = await preparer.PrepareAsync();
        }
        catch (OperationCanceledException)
        {
            launchRequest = null;
        }
        catch (Exception e)
        {
            logger.Error($"Target preparer '{preparer.DisplayName}' failed to prepare: {e.Message}", e);
            launchRequest = null;
        }

        // The silent failure has to clean up as well: the preparer may already have brought up
        // half the chain before it gave up.
        _activePreparer = preparer;

        if (launchRequest == null)
        {
            await CleanupPreparerAsync();
            return false;
        }

        if (await StartCoreAsync(launchRequest)) return true;

        // A failure further down went through StopAsync and has thereby already released the
        // preparer; then this is a no-op. The cases that bail out before the start -
        // no launcher, CanLaunch declined - are only covered by this call.
        await CleanupPreparerAsync();
        return false;
    }

    private async Task<bool> StartCoreAsync(DebugLaunchRequest launchRequest)
    {
        var launcher = _sessionLaunchers.FirstOrDefault(x => x.Id == launchRequest.BackendId);
        if (launcher == null)
        {
            logger.Error($"No session launcher with id '{launchRequest.BackendId}' is registered.");
            return false;
        }

        if (!launcher.CanLaunch(launchRequest))
        {
            logger.Error($"Session launcher '{launcher.Id}' cannot launch this request.");
            return false;
        }

        IDebugSession session;
        try
        {
            session = launcher.CreateSession(launchRequest);
        }
        catch (Exception e)
        {
            logger.Error($"Session launcher '{launcher.Id}' could not create a session: {e.Message}", e);
            return false;
        }

        session.StateChanged += OnSessionStateChanged;
        session.Exited += OnSessionExited;

        _session = session;
        _projectRoot = GetActiveProjectRoot();
        State = DebugSessionState.Empty;
        TargetProfile = launchRequest.TargetProfile ?? DebugTargetProfile.Default;
        RaiseStateChanged();

        if (!await session.StartAsync())
        {
            // Without this cleanup a half-started GDB process would be left behind and the
            // UI would still have a session that answers nothing.
            await StopAsync();
            return false;
        }

        var sourceFiles = await session.GetSourceFilesAsync();
        _sourceFiles = sourceFiles == null
            ? null
            : await Task.Run(() => sourceFiles
                .SelectMany(x => new[] { NormalizePath(x), NormalizePath(ProcessHelper.ResolveRealPath(x)) })
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        // Arm the breakpoints first, then let it run - the other way round the program runs
        // past exactly the places where the user wanted to stop.
        await ResyncBreakpointsAsync();

        _breakpointStore.Breakpoints.CollectionChanged += OnBreakpointsChanged;

        await session.RunAsync();

        return true;
    }

    public async Task StopAsync()
    {
        var session = _session;

        if (session != null)
        {
            _breakpointStore.Breakpoints.CollectionChanged -= OnBreakpointsChanged;
            session.StateChanged -= OnSessionStateChanged;
            session.Exited -= OnSessionExited;

            // Clear early: a second, concurrent call then bails out again immediately.
            _session = null;

            // Stop waits for the backend to end. On the UI thread that would be a visible
            // freeze, hence moved off it.
            await Task.Run(session.Stop);

            State = DebugSessionState.Empty;
            TargetProfile = DebugTargetProfile.Default;
            _armed.Clear();
            _refused.Clear();
            _sourceFiles = null;
            _projectRoot = null;
            _breakpointStore.IsTargetRunning = false;
            _breakpointStore.CurrentBreakPoint = null;

            // Without a target nobody says anything about the breakpoints any more -> a hollow dot
            // would from here on be a claim without basis.
            _breakpointStore.ResetVerification();

            RaiseStateChanged();
        }

        // After the backend, not before: while GDB detaches from its target, the stub must
        // still be up.
        await CleanupPreparerAsync();
    }

    // Releases what the preparer of the running session has claimed. Can be called more than once -
    // the second call finds nothing left.
    private async Task CleanupPreparerAsync()
    {
        if (_activePreparer is not { } preparer) return;

        _activePreparer = null;

        try
        {
            await preparer.CleanupAsync();
        }
        catch (Exception e)
        {
            // A preparer that throws during cleanup must not hold up the end of the session -
            // the session is already torn down at this point.
            logger.Error($"Target preparer '{preparer.DisplayName}' failed to clean up: {e.Message}", e);
        }
    }

    public Task ContinueAsync()
    {
        var session = _session;
        return session == null ? Task.CompletedTask : session.ContinueAsync();
    }

    public Task PauseAsync()
    {
        var session = _session;
        return session == null ? Task.CompletedTask : session.PauseAsync();
    }

    public Task StepIntoAsync()
    {
        var session = _session;
        return session == null ? Task.CompletedTask : session.StepIntoAsync();
    }

    public Task StepOverAsync()
    {
        var session = _session;
        return session == null ? Task.CompletedTask : session.StepOverAsync();
    }

    public Task StepOutAsync()
    {
        var session = _session;
        return session == null ? Task.CompletedTask : session.StepOutAsync();
    }

    public Task<string?> ReadMemoryAsync(string address, int byteCount)
    {
        var session = _session;

        // While the target runs, the backend accepts no read request. Sending it anyway
        // would only produce one error message per address in the console.
        if (session == null || State.IsRunning) return Task.FromResult<string?>(null);

        return session.ReadMemoryAsync(address, byteCount);
    }

    public Task<DebugWriteResult> WriteRegisterAsync(string name, ulong value)
    {
        return WriteAsync(session => session.WriteRegisterAsync(name, value));
    }

    public Task<DebugWriteResult> WriteMemoryAsync(string address, byte[] bytes)
    {
        return WriteAsync(session => session.WriteMemoryAsync(address, bytes));
    }

    public Task<DebugWriteResult> WriteVariableAsync(string name, long value)
    {
        return WriteAsync(session => session.WriteVariableAsync(name, value));
    }

    private Task<DebugWriteResult> WriteAsync(Func<IDebugSession, Task<DebugWriteResult>> write)
    {
        var session = _session;

        if (session == null) return Task.FromResult(DebugWriteResult.Failed("No debug session is running."));
        if (State.IsRunning) return Task.FromResult(DebugWriteResult.Failed("The target is running."));

        return write(session);
    }

    public Task SendRawCommandAsync(string command)
    {
        if (_session == null || string.IsNullOrWhiteSpace(command)) return Task.CompletedTask;
        return _session.SendRawCommandAsync(command);
    }

    // Keeps the editor's breakpoints and those of the running target in sync, so that a
    // red dot set during the session takes effect immediately.
    private void OnBreakpointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_session == null) return;

        _ = SyncBreakpointsAsync(e.OldItems?.Cast<BreakPoint>().ToArray() ?? []);
    }

    private async Task SyncBreakpointsAsync(IReadOnlyList<BreakPoint> removed)
    {
        var session = _session;
        if (session == null) return;

        try
        {
            foreach (var breakpoint in removed)
            {
                var wasArmed = _armed.Remove(breakpoint);
                _refused.Remove(breakpoint);

                if (wasArmed) await session.RemoveBreakpointAsync(breakpoint);
            }

            await ResyncBreakpointsAsync();
        }
        catch (Exception e)
        {
            // The call runs without an awaiter -> an exception would otherwise be lost silently.
            logger.Error($"Could not synchronise the breakpoints with the target: {e.Message}", e);
        }
    }

    private async Task ResyncBreakpointsAsync()
    {
        var session = _session;
        if (session == null || State.IsRunning) return;

        var waiting = 0;
        var rejected = 0;

        foreach (var breakpoint in _breakpointStore.Breakpoints.ToArray())
        {
            if (_armed.Contains(breakpoint) || _refused.Contains(breakpoint)) continue;

            // Another project's breakpoint is not this session's business: not armed, but not
            // reported as refused either - its dot stays as it is.
            if (!BelongsToSessionProject(breakpoint.File)) continue;

            var wasVerified = breakpoint.IsVerified;

            if (!BelongsToLoadedProgram(breakpoint.File))
            {
                // Refused because it belongs to a different program
                _refused.Add(breakpoint);
                PublishVerification(breakpoint, false);
                if (wasVerified) rejected++;
                continue;
            }

            if (!HasFreeSlot())
            {
                PublishVerification(breakpoint, false);
                if (wasVerified) waiting++;
                continue;
            }

            if (await TryArmAsync(session, breakpoint)) continue;

            _refused.Add(breakpoint);
            if (wasVerified) rejected++;
        }

        if (waiting > 0) NotifyWaiting(waiting);
        if (rejected > 0) NotifyRejected(rejected);
    }

    private async Task<bool> TryArmAsync(IDebugSession session, BreakPoint breakpoint)
    {
        var verified = await session.SetBreakpointAsync(breakpoint);

        if (verified) _armed.Add(breakpoint);

        PublishVerification(breakpoint, verified);
        return verified;
    }

    private bool HasFreeSlot()
    {
        if (TargetProfile.MaxBreakpoints is not { } limit) return true;

        return _armed.Count < limit;
    }

    private bool BelongsToSessionProject(string file) =>
        _projectRoot == null || ProjectScope.Contains(_projectRoot, file);

    private string? GetActiveProjectRoot()
    {
        try
        {
            return serviceProvider.Resolve<IProjectExplorerService>().ActiveProject?.RootFolderPath;
        }
        catch (Exception e)
        {
            // Without the explorer (e.g. a host without projects) the session simply is not scoped.
            logger.Warning($"Could not determine the active project: {e.Message}");
            return null;
        }
    }

    private bool BelongsToLoadedProgram(string file)
    {
        if (_sourceFiles is not { Count: > 0 } files) return true;

        return files.Contains(NormalizePath(file)) ||
               files.Contains(NormalizePath(ProcessHelper.ResolveRealPath(file)));
    }

    // GDB reports source files with symbolic links resolved while the editor uses the project path,
    // so a plain string comparison misses e.g. a project folder reached through a linked home directory.
    internal static bool IsSameFile(string a, string b)
    {
        if (string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase)) return true;

        return string.Equals(NormalizePath(ProcessHelper.ResolveRealPath(a)),
            NormalizePath(ProcessHelper.ResolveRealPath(b)), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    // Marshalled to the UI thread: the result comes from the session's reader thread, and the
    // redraw of the margin in every open editor depends on it.
    private void PublishVerification(BreakPoint breakpoint, bool verified)
    {
        Dispatcher.UIThread.Post(() => _breakpointStore.SetVerified(breakpoint, verified));
    }

    // The hollow dot in the margin only means something if you know the notation.
    // Hence a short notification in addition.
    // Warning, not error: the session keeps running and the breakpoint stays in place -
    // it is a limitation, not an abort. The classification also brings the colour from
    // the host application's notification theme, instead of it being fixed here.
    // The reason is deliberately phrased as a possibility: the protocol reports the failure,
    // not its cause.
    private void NotifyWaiting(int waiting)
    {
        var more = waiting == 1 ? "One breakpoint is" : $"{waiting} breakpoints are";

        serviceProvider.Resolve<IWindowService>().ShowNotification(
            "Breakpoint limit reached",
            $"{more} waiting for a free slot on the target and shown as a grey ring. Remove one and " +
            "the next takes its place.",
            NotificationType.Warning);
    }

    private void NotifyRejected(int rejected)
    {
        var what = rejected == 1 ? "a breakpoint" : $"{rejected} breakpoints";

        serviceProvider.Resolve<IWindowService>().ShowNotification(
            "Breakpoint not set",
            $"The target did not accept {what}, now shown as a grey ring.",
            NotificationType.Warning);
    }

    private void OnSessionStateChanged(object? sender, DebugSessionState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // A message from the old session after a stop must no longer switch the UI over.
            if (!ReferenceEquals(sender, _session)) return;

            State = state;
            _breakpointStore.IsTargetRunning = state.IsRunning;
            _breakpointStore.CurrentBreakPoint = FindCurrentBreakpoint(state);
            RaiseStateChanged();

            if (!state.IsRunning) _ = SyncBreakpointsAsync([]);
        });
    }

    // The line the target stands on - even if no breakpoint is set there, because after a
    // single step the user still wants to see where they are.
    private BreakPoint? FindCurrentBreakpoint(DebugSessionState state)
    {
        if (state.IsRunning) return null;
        if (state.CurrentFrame is not { File: { Length: > 0 } file, Line: > 0 } frame) return null;

        return _breakpointStore.Breakpoints.FirstOrDefault(x => x.Line == frame.Line && IsSameFile(x.File, file))
               ?? new BreakPoint { File = file, Line = frame.Line };
    }

    private void OnSessionExited(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _session)) return;
            _ = StopAsync();
        });
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
