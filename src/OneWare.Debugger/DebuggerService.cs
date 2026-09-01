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
    private readonly List<IDebugAdapter> _adapters = [];
    private readonly BreakpointStore _breakpoints = BreakpointStore.Instance;
    private readonly List<IDebugLaunchProvider> _launchProviders = [];

    private IDebugLaunchProvider? _activeProvider;

    private IDebugSession? _session;

    public IReadOnlyList<IDebugAdapter> Adapters => _adapters;

    public IReadOnlyList<IDebugLaunchProvider> LaunchProviders => _launchProviders;

    public IDebugSession? CurrentSession => _session;

    public DebugSessionState State { get; private set; } = DebugSessionState.Empty;

    public bool IsActive => _session != null;

    // Kommt mit der Startanforderung und gilt fuer die Dauer der Sitzung. Ohne Sitzung steht hier
    // das byteadressierte Standardprofil -> der Memory-Reiter rechnet dann wie bisher mit 1.
    public DebugMemoryProfile MemoryProfile { get; private set; } = DebugMemoryProfile.Default;

    public event EventHandler? StateChanged;

    public void RegisterAdapter<T>() where T : IDebugAdapter
    {
        var adapter = serviceProvider.Resolve<T>();

        if (_adapters.Any(x => x.Id == adapter.Id))
        {
            logger.Warning($"A debug adapter with id '{adapter.Id}' is already registered - ignoring.");
            return;
        }

        _adapters.Add(adapter);
    }

    public void RegisterLaunchProvider<T>() where T : IDebugLaunchProvider
    {
        var provider = serviceProvider.Resolve<T>();

        if (_launchProviders.Any(x => x.GetType() == provider.GetType()))
        {
            logger.Warning($"A target preparer of type '{provider.GetType().Name}' is already registered - ignoring.");
            return;
        }

        _launchProviders.Add(provider);
    }

    public async Task<bool> StartAsync(DebugLaunchRequest launchRequest)
    {
        await StopAsync();

        return await StartCoreAsync(launchRequest);
    }

    public async Task<bool> StartAsync(IDebugLaunchProvider provider, CancellationToken ct = default)
    {
        // Erst die laufende Sitzung abraeumen, dann vorbereiten: der Vorbereiter greift auf
        // dieselben Betriebsmittel zu wie die vorige Sitzung - eine serielle Schnittstelle etwa
        // laesst sich nicht zweimal oeffnen.
        await StopAsync();

        DebugLaunchRequest? launchRequest;
        try
        {
            launchRequest = await provider.PrepareAsync(ct);
        }
        catch (OperationCanceledException)
        {
            launchRequest = null;
        }
        catch (Exception e)
        {
            logger.Error($"Target preparer '{provider.DisplayName}' failed to prepare: {e.Message}", e);
            launchRequest = null;
        }

        // Auch der stille Fehlschlag muss aufraeumen: der Vorbereiter kann die halbe Kette bereits
        // hochgefahren haben, bevor er aufgab.
        _activeProvider = provider;

        if (launchRequest == null)
        {
            await CleanupProviderAsync();
            return false;
        }

        // Uebergangsgeruest, Gegenstueck zum Vier-Argument-Konstruktor am DebugLaunchRequest:
        // das einzige existierende Plugin baut gegen das veroeffentlichte Paket und kann noch
        // kein Profil mitgeben. Solange traegt der Kern hier die Geometrie dieses einen Ziels
        // nach - nur auf dem Vorbereiter-Weg, der Start ueber Tools > Debugger bleibt
        // byteadressiert. Faellt weg, sobald die erste Anforderung ein eigenes Profil bringt.
        if (launchRequest.MemoryProfile == null)
            launchRequest = launchRequest with
            {
                MemoryProfile = new DebugMemoryProfile
                {
                    AddressableUnitBytes = 2,
                    DefaultLength = 2,
                    AddressWatermark = "Wortadresse im SVNR-RAM, z. B. 0x0 - 0x3FF"
                }
            };

        if (await StartCoreAsync(launchRequest)) return true;

        // Ein Fehlschlag unterhalb ist ueber StopAsync gelaufen und hat den Vorbereiter damit
        // schon abgeraeumt; dann ist das hier ein Leerlauf. Die Faelle, die vor dem Start
        // aussteigen - kein Adapter, CanLaunch verneint - deckt erst dieser Aufruf ab.
        await CleanupProviderAsync();
        return false;
    }

    private async Task<bool> StartCoreAsync(DebugLaunchRequest launchRequest)
    {
        var adapter = _adapters.FirstOrDefault(x => x.Id == launchRequest.AdapterId);
        if (adapter == null)
        {
            logger.Error($"No debug adapter with id '{launchRequest.AdapterId}' is registered.");
            return false;
        }

        if (!adapter.CanLaunch(launchRequest))
        {
            logger.Error($"Debug adapter '{adapter.Id}' cannot launch this request.");
            return false;
        }

        IDebugSession session;
        try
        {
            session = adapter.CreateSession(launchRequest);
        }
        catch (Exception e)
        {
            logger.Error($"Debug adapter '{adapter.Id}' could not create a session: {e.Message}", e);
            return false;
        }

        session.StateChanged += OnSessionStateChanged;
        session.Exited += OnSessionExited;

        _session = session;
        State = DebugSessionState.Empty;
        MemoryProfile = launchRequest.MemoryProfile ?? DebugMemoryProfile.Default;
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
        foreach (var breakpoint in _breakpoints.Breakpoints.ToArray())
            PublishVerification(breakpoint, await session.SetBreakpointAsync(breakpoint));

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
            MemoryProfile = DebugMemoryProfile.Default;
            _breakpoints.CurrentBreakPoint = null;

            // Ohne Ziel sagt niemand mehr etwas ueber die Breakpoints aus -> ein hohler Punkt
            // waere ab hier eine Behauptung ohne Grundlage.
            _breakpoints.ResetVerification();

            RaiseStateChanged();
        }

        // Nach dem Backend, nicht davor: solange sich GDB von seinem Ziel loest, muss der Stub
        // noch stehen.
        await CleanupProviderAsync();
    }

    // Gibt frei, was der Vorbereiter der laufenden Sitzung belegt hat. Mehrfach aufrufbar -
    // der zweite Aufruf findet nichts mehr vor.
    private async Task CleanupProviderAsync()
    {
        if (_activeProvider is not { } provider) return;

        _activeProvider = null;

        try
        {
            await provider.CleanupAsync();
        }
        catch (Exception e)
        {
            // Ein Vorbereiter, der beim Aufraeumen wirft, darf das Ende der Sitzung nicht
            // aufhalten - die Sitzung ist an dieser Stelle bereits abgebaut.
            logger.Error($"Target preparer '{provider.DisplayName}' failed to clean up: {e.Message}", e);
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
        var session = _session;
        if (session == null) return;

        if (e.NewItems != null)
            foreach (BreakPoint breakpoint in e.NewItems)
                _ = ArmAsync(session, breakpoint);

        if (e.OldItems == null) return;
        {
            foreach (BreakPoint breakpoint in e.OldItems)
                _ = session.RemoveBreakpointAsync(breakpoint);
        }
    }

    // Macht einen waehrend der Sitzung gesetzten Breakpoint am Ziel scharf und haelt fest, ob
    // das Ziel ihn angenommen hat. Der Rueckgabewert wurde bisher verworfen - damit blieb der
    // Punkt rot, auch wenn das Ziel ihn abgelehnt hatte.
    private async Task ArmAsync(IDebugSession session, BreakPoint breakpoint)
    {
        try
        {
            PublishVerification(breakpoint, await session.SetBreakpointAsync(breakpoint));
        }
        catch (Exception e)
        {
            // Der Aufruf laeuft ohne Erwartenden -> eine Ausnahme ginge sonst still verloren.
            logger.Error($"Could not arm the breakpoint at {breakpoint.File}:{breakpoint.Line}: {e.Message}", e);
        }
    }

    // Auf den UI-Thread gebracht: das Ergebnis kommt vom Lesethread der Sitzung, und daran
    // haengt das Neuzeichnen der Randspalte in jedem offenen Editor.
    private void PublishVerification(BreakPoint breakpoint, bool verified)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Vor dem Setzen vergleichen -> gemeldet wird nur der Wechsel. Beim
            // Scharfmachen zum Sitzungsstart liefe sonst fuer jeden bereits bekannten
            // abgelehnten Haltepunkt erneut eine Meldung auf.
            var rejectedNow = breakpoint.IsVerified && !verified;

            _breakpoints.SetVerified(breakpoint, verified);

            if (rejectedNow) NotifyRejected(breakpoint);
        });
    }

    // Der hohle Punkt in der Randspalte sagt nur etwas, wenn man die Schreibweise kennt.
    // Deshalb zusaetzlich eine Kurzmeldung.
    // Warnung, nicht Fehler: die Sitzung laeuft weiter und der Haltepunkt bleibt stehen -
    // es ist eine Einschraenkung, kein Abbruch. Ueber die Einstufung kommt zugleich die
    // Farbe aus dem Benachrichtigungsthema der Host-Anwendung, statt hier festgelegt zu
    // werden.
    // Der Grund steht bewusst im Konjunktiv: das Protokoll meldet das Scheitern, nicht
    // dessen Ursache.
    private void NotifyRejected(BreakPoint breakpoint)
    {
        var file = string.IsNullOrEmpty(breakpoint.File) ? "?" : Path.GetFileName(breakpoint.File);

        serviceProvider.Resolve<IWindowService>().ShowNotification(
            "Breakpoint not set",
            $"The target did not accept the breakpoint at {file}:{breakpoint.Line}. " +
            "It may have run out of breakpoint slots.",
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
            _breakpoints.CurrentBreakPoint = FindCurrentBreakpoint(state);
            RaiseStateChanged();
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
