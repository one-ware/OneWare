using System.Collections.Specialized;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Debugger;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

/// <summary>
///     Haelt die registrierten Backends und besitzt die eine Session, die gleichzeitig laufen kann.
/// </summary>
/// <remarks>
///     Alles, was hier nach aussen geht, ist auf den UI-Thread gebracht. Die Session meldet sich
///     aus ihrem Lesethread; das an genau einer Stelle einzusammeln ist einfacher, als es jedem
///     Panel einzeln aufzutragen.
/// </remarks>
public class DebuggerService(IServiceProvider serviceProvider, ILogger logger) : IDebuggerService
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
            logger.Warning($"A launch provider of type '{provider.GetType().Name}' is already registered - ignoring.");
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
            logger.Error($"Launch provider '{provider.DisplayName}' failed to prepare: {e.Message}", e);
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
            await session.SetBreakpointAsync(breakpoint);

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
            _breakpoints.CurrentBreakPoint = null;
            RaiseStateChanged();
        }

        // Nach dem Backend, nicht davor: solange sich GDB von seinem Ziel loest, muss der Stub
        // noch stehen.
        await CleanupProviderAsync();
    }

    /// <summary>
    ///     Gibt frei, was der Vorbereiter der laufenden Sitzung belegt hat. Mehrfach aufrufbar -
    ///     der zweite Aufruf findet nichts mehr vor.
    /// </summary>
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
            logger.Error($"Launch provider '{provider.DisplayName}' failed to clean up: {e.Message}", e);
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

    /// <summary>
    ///     Haelt die Breakpoints des Editors und die des laufenden Ziels zusammen, damit ein
    ///     waehrend der Sitzung gesetzter roter Punkt sofort greift.
    /// </summary>
    private void OnBreakpointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var session = _session;
        if (session == null) return;

        if (e.NewItems != null)
            foreach (BreakPoint breakpoint in e.NewItems)
                _ = session.SetBreakpointAsync(breakpoint);

        if (e.OldItems == null) return;
        {
            foreach (BreakPoint breakpoint in e.OldItems)
                _ = session.RemoveBreakpointAsync(breakpoint);
        }
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

    /// <summary>
    ///     Die Zeile, auf der das Ziel steht - auch wenn dort gar kein Breakpoint gesetzt ist, denn
    ///     nach einem Einzelschritt will der Nutzer trotzdem sehen, wo er ist.
    /// </summary>
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
