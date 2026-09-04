using System.Collections.Specialized;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

// Haelt die registrierten Backends und besitzt die eine Session, die gleichzeitig laufen kann.
// Alles, was hier nach aussen geht, ist auf den UI-Thread gebracht. Die Session meldet sich
// aus ihrem Lesethread; das an genau einer Stelle einzusammeln ist einfacher, als es jedem
// Panel einzeln aufzutragen.
public class DebuggerService(ICompositeServiceProvider serviceProvider, ILogger logger) : IDebuggerService
{
    private readonly List<IDebugSessionLauncher> _sessionLaunchers = [];
    private readonly HashSet<BreakPoint> _armed = [];
    private readonly HashSet<BreakPoint> _refused = [];
    private readonly BreakpointStore _breakpoints = BreakpointStore.Instance;
    private readonly List<IDebugTargetPreparer> _targetPreparers = [];

    private IDebugTargetPreparer? _activePreparer;

    private IDebugSession? _session;

    public IReadOnlyList<IDebugSessionLauncher> SessionLaunchers => _sessionLaunchers;

    public IReadOnlyList<IDebugTargetPreparer> TargetPreparers => _targetPreparers;

    public IDebugSession? CurrentSession => _session;

    public DebugSessionState State { get; private set; } = DebugSessionState.Empty;

    public bool IsActive => _session != null;

    // Kommt mit der Startanforderung und gilt fuer die Dauer der Sitzung. Ohne Sitzung steht hier
    // das byteadressierte Standardprofil -> der Memory-Reiter rechnet dann wie bisher mit 1.
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
        // Erst die laufende Sitzung abraeumen, dann vorbereiten: der Vorbereiter greift auf
        // dieselben Betriebsmittel zu wie die vorige Sitzung - eine serielle Schnittstelle etwa
        // laesst sich nicht zweimal oeffnen.
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

        // Auch der stille Fehlschlag muss aufraeumen: der Vorbereiter kann die halbe Kette bereits
        // hochgefahren haben, bevor er aufgab.
        _activePreparer = preparer;

        if (launchRequest == null)
        {
            await CleanupPreparerAsync();
            return false;
        }

        if (await StartCoreAsync(launchRequest)) return true;

        // Ein Fehlschlag unterhalb ist ueber StopAsync gelaufen und hat den Vorbereiter damit
        // schon abgeraeumt; dann ist das hier ein Leerlauf. Die Faelle, die vor dem Start
        // aussteigen - kein Adapter, CanLaunch verneint - deckt erst dieser Aufruf ab.
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
        State = DebugSessionState.Empty;
        TargetProfile = launchRequest.TargetProfile ?? DebugTargetProfile.Default;
        RaiseStateChanged();

        if (!await session.StartAsync())
        {
            // Ohne dieses Aufraeumen bliebe ein halb hochgefahrener GDB-Prozess zurueck und die
            // Oberflaeche haette weiter eine Session, die nichts beantwortet.
            await StopAsync();
            return false;
        }

        // Erst die Breakpoints scharf machen, dann laufen lassen - andersherum rennt das Programm
        // an genau den Stellen vorbei, an denen der Nutzer halten wollte.
        await ResyncBreakpointsAsync();

        _breakpoints.Breakpoints.CollectionChanged += OnBreakpointsChanged;

        await session.RunAsync();

        return true;
    }

    public async Task StopAsync()
    {
        var session = _session;

        if (session != null)
        {
            _breakpoints.Breakpoints.CollectionChanged -= OnBreakpointsChanged;
            session.StateChanged -= OnSessionStateChanged;
            session.Exited -= OnSessionExited;

            // Frueh leeren: ein zweiter, gleichzeitiger Aufruf steigt damit sofort wieder aus.
            _session = null;

            // Stop wartet auf das Ende des Backends. Auf dem UI-Thread waere das ein sichtbares
            // Einfrieren, deshalb ausgelagert.
            await Task.Run(session.Stop);

            State = DebugSessionState.Empty;
            TargetProfile = DebugTargetProfile.Default;
            _armed.Clear();
            _refused.Clear();
            _breakpoints.IsTargetRunning = false;
            _breakpoints.CurrentBreakPoint = null;

            // Ohne Ziel sagt niemand mehr etwas ueber die Breakpoints aus -> ein hohler Punkt
            // waere ab hier eine Behauptung ohne Grundlage.
            _breakpoints.ResetVerification();

            RaiseStateChanged();
        }

        // Nach dem Backend, nicht davor: solange sich GDB von seinem Ziel loest, muss der Stub
        // noch stehen.
        await CleanupPreparerAsync();
    }

    // Gibt frei, was der Vorbereiter der laufenden Sitzung belegt hat. Mehrfach aufrufbar -
    // der zweite Aufruf findet nichts mehr vor.
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
            // Ein Vorbereiter, der beim Aufraeumen wirft, darf das Ende der Sitzung nicht
            // aufhalten - die Sitzung ist an dieser Stelle bereits abgebaut.
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

        // Waehrend das Ziel laeuft, nimmt das Backend keine Leseanfrage an. Sie trotzdem zu
        // schicken haette nur eine Fehlermeldung je Adresse in der Console zur Folge.
        if (session == null || State.IsRunning) return Task.FromResult<string?>(null);

        return session.ReadMemoryAsync(address, byteCount);
    }

    public Task SendRawCommandAsync(string command)
    {
        if (_session == null || string.IsNullOrWhiteSpace(command)) return Task.CompletedTask;
        return _session.SendRawCommandAsync(command);
    }

    // Haelt die Breakpoints des Editors und die des laufenden Ziels zusammen, damit ein
    // waehrend der Sitzung gesetzter roter Punkt sofort greift.
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
                _armed.Remove(breakpoint);
                _refused.Remove(breakpoint);
                await session.RemoveBreakpointAsync(breakpoint);
            }

            await ResyncBreakpointsAsync();
        }
        catch (Exception e)
        {
            // Der Aufruf laeuft ohne Erwartenden -> eine Ausnahme ginge sonst still verloren.
            logger.Error($"Could not synchronise the breakpoints with the target: {e.Message}", e);
        }
    }

    private bool HasFreeSlot()
    {
        if (TargetProfile.MaxBreakpoints is not { } limit) return true;

        return _armed.Count < limit;
    }

    private async Task ResyncBreakpointsAsync()
    {
        var session = _session;
        if (session == null || State.IsRunning) return;

        var waiting = 0;
        var rejected = 0;

        foreach (var breakpoint in _breakpoints.Breakpoints.ToArray())
        {
            if (_armed.Contains(breakpoint) || _refused.Contains(breakpoint)) continue;

            var wasVerified = breakpoint.IsVerified;

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

    // Auf den UI-Thread gebracht: das Ergebnis kommt vom Lesethread der Sitzung, und daran
    // haengt das Neuzeichnen der Randspalte in jedem offenen Editor.
    private void PublishVerification(BreakPoint breakpoint, bool verified)
    {
        Dispatcher.UIThread.Post(() => _breakpoints.SetVerified(breakpoint, verified));
    }

    // Der hohle Punkt in der Randspalte sagt nur etwas, wenn man die Schreibweise kennt.
    // Deshalb zusaetzlich eine Kurzmeldung.
    // Warnung, nicht Fehler: die Sitzung laeuft weiter und der Haltepunkt bleibt stehen -
    // es ist eine Einschraenkung, kein Abbruch. Ueber die Einstufung kommt zugleich die
    // Farbe aus dem Benachrichtigungsthema der Host-Anwendung, statt hier festgelegt zu
    // werden.
    // Der Grund steht bewusst im Konjunktiv: das Protokoll meldet das Scheitern, nicht
    // dessen Ursache.
    private void NotifyWaiting(int waiting)
    {
        var more = waiting == 1 ? "one more is" : $"{waiting} more are";

        serviceProvider.Resolve<IWindowService>().ShowNotification(
            "Breakpoint limit reached",
            $"The target holds {TargetProfile.MaxBreakpoints} breakpoints at once, {more} waiting for a " +
            "free slot and shown as a grey ring. Remove one and the next takes its place.",
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
            // Eine Meldung der alten Session nach einem Stop darf die Oberflaeche nicht mehr
            // umschalten.
            if (!ReferenceEquals(sender, _session)) return;

            State = state;
            _breakpoints.IsTargetRunning = state.IsRunning;
            _breakpoints.CurrentBreakPoint = FindCurrentBreakpoint(state);
            RaiseStateChanged();

            if (!state.IsRunning) _ = SyncBreakpointsAsync([]);
        });
    }

    // Die Zeile, auf der das Ziel steht - auch wenn dort gar kein Breakpoint gesetzt ist, denn
    // nach einem Einzelschritt will der Nutzer trotzdem sehen, wo er ist.
    private BreakPoint? FindCurrentBreakpoint(DebugSessionState state)
    {
        if (state.IsRunning) return null;
        if (state.CurrentFrame is not { File: { Length: > 0 } file, Line: > 0 } frame) return null;

        return _breakpoints.Breakpoints.FirstOrDefault(x =>
                   string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase) && x.Line == frame.Line)
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
