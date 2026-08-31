using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

/// <summary>
/// The service a plugin resolves in order to take part in debugging. The dependency runs one
/// way only — plugins depend on these contracts, the core never learns that a given plugin
/// exists.
/// </summary>
public interface IDebuggerService
{
    /// <summary>
    /// Registered backends, including the core's own.
    /// </summary>
    public IReadOnlyList<IDebugAdapter> Adapters { get; }

    /// <summary>
    /// Registered launch providers. Whoever fits the active project shows up in the launch
    /// selection of the debug panel.
    /// </summary>
    public IReadOnlyList<IDebugLaunchProvider> LaunchProviders { get; }

    /// <summary>
    /// The active session, or <see langword="null"/> if none is running.
    /// </summary>
    public IDebugSession? CurrentSession { get; }

    /// <summary>
    /// State of the active session, or <see cref="DebugSessionState.Empty"/> if none is running.
    /// </summary>
    public DebugSessionState State { get; }

    /// <summary>
    /// <see langword="true"/> while a session is running. Gates the breakpoint margin in the
    /// editor.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Memory geometry of the target of the active session, or
    /// <see cref="DebugMemoryProfile.Default"/> when none is running. Taken from the request the
    /// session was started with. Implemented by default so that adding it breaks no implementer.
    /// </summary>
    public DebugMemoryProfile MemoryProfile => DebugMemoryProfile.Default;

    /// <summary>
    /// Fired when <see cref="State"/>, <see cref="CurrentSession"/>, or <see cref="IsActive"/>
    /// changed. Always raised on the UI thread, so handlers can touch bound collections directly.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Registers an adapter. Resolved from the container — the implementation gets constructor
    /// injection like any other service.
    /// </summary>
    public void RegisterAdapter<T>() where T : IDebugAdapter;

    /// <summary>
    /// Registers a launch provider. Resolved from the container like adapters.
    /// </summary>
    public void RegisterLaunchProvider<T>() where T : IDebugLaunchProvider;

    /// <summary>
    /// Starts a session, arms the breakpoints currently set in the editor and runs the program.
    /// Returns <see langword="false"/> if no adapter accepted the request or the backend did not
    /// come up; nothing is left running in that case.
    /// </summary>
    public Task<bool> StartAsync(DebugLaunchRequest launchRequest);

    /// <summary>
    /// Calls <see cref="IDebugLaunchProvider.PrepareAsync"/> first, then starts with the
    /// returned request. <see cref="IDebugLaunchProvider.CleanupAsync"/> runs as soon as the
    /// session ends, no matter how it ended.
    /// </summary>
    public Task<bool> StartAsync(IDebugLaunchProvider provider, CancellationToken ct = default);

    /// <summary>
    /// Ends the active session. Does nothing if none is running.
    /// </summary>
    public Task StopAsync();

    /// <summary>
    /// Forwards to <see cref="IDebugSession.ContinueAsync"/> on <see cref="CurrentSession"/>.
    /// Does nothing when no session is active.
    /// </summary>
    public Task ContinueAsync();

    /// <summary>
    /// Forwards to <see cref="IDebugSession.PauseAsync"/> on <see cref="CurrentSession"/>.
    /// Does nothing when no session is active.
    /// </summary>
    public Task PauseAsync();

    /// <summary>
    /// Forwards to <see cref="IDebugSession.StepIntoAsync"/> on <see cref="CurrentSession"/>.
    /// Does nothing when no session is active.
    /// </summary>
    public Task StepIntoAsync();

    /// <summary>
    /// Forwards to <see cref="IDebugSession.StepOverAsync"/> on <see cref="CurrentSession"/>.
    /// Does nothing when no session is active.
    /// </summary>
    public Task StepOverAsync();

    /// <summary>
    /// Forwards to <see cref="IDebugSession.StepOutAsync"/> on <see cref="CurrentSession"/>.
    /// Does nothing when no session is active.
    /// </summary>
    public Task StepOutAsync();

    /// <summary>
    /// Forwards to <see cref="IDebugSession.ReadMemoryAsync"/> on <see cref="CurrentSession"/>.
    /// Returns <see langword="null"/> when no session is active.
    /// </summary>
    public Task<string?> ReadMemoryAsync(string address, int byteCount);

    /// <summary>
    /// Forwards to <see cref="IDebugSession.SendRawCommandAsync"/> on
    /// <see cref="CurrentSession"/>. Does nothing when no session is active.
    /// </summary>
    public Task SendRawCommandAsync(string command);
}
