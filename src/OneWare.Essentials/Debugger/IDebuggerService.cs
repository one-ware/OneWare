namespace OneWare.Essentials.Debugger;

/// <summary>
/// Orchestrates debugging: holds the registered backends and owns the one session that can be
/// active at a time.
/// </summary>
/// <remarks>
/// This is the service a plugin resolves in order to take part in debugging. The dependency runs
/// one way only — plugins depend on these contracts, the core never learns that a given plugin
/// exists.
/// <para>
/// The control commands mirror <see cref="IDebugSession"/> and forward to
/// <see cref="CurrentSession"/>, doing nothing when there is none. Panels therefore bind to this
/// service alone and never have to rebind when a session starts or ends.
/// </para>
/// </remarks>
public interface IDebuggerService
{
    /// <summary>
    /// Registered debug backends, the core's own included.
    /// </summary>
    public IReadOnlyList<IDebugAdapter> Adapters { get; }

    /// <summary>
    /// The active session, or <c>null</c> if none is running.
    /// </summary>
    public IDebugSession? CurrentSession { get; }

    /// <summary>
    /// State of the active session, or <see cref="DebugSessionState.Empty"/> if none is running.
    /// </summary>
    public DebugSessionState State { get; }

    /// <summary>
    /// Whether a session is running. This is what gates the breakpoint margin in the editor.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Fired when <see cref="State"/>, <see cref="CurrentSession"/> or <see cref="IsActive"/>
    /// changed. Always raised on the UI thread, so handlers can touch bound collections directly.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Registers a debug backend. Resolved from the container, so the implementation gets
    /// constructor injection like any other service.
    /// </summary>
    public void RegisterAdapter<T>() where T : IDebugAdapter;

    /// <summary>
    /// Starts a session, arms the breakpoints currently set in the editor and runs the program.
    /// </summary>
    /// <returns><c>false</c> if no registered adapter accepted the request or the backend did not
    /// come up. Nothing is left running in that case.</returns>
    public Task<bool> StartAsync(DebugLaunchRequest launchRequest);

    /// <summary>
    /// Ends the active session. Does nothing if none is running.
    /// </summary>
    public Task StopAsync();

    /// <inheritdoc cref="IDebugSession.ContinueAsync"/>
    public Task ContinueAsync();

    /// <inheritdoc cref="IDebugSession.PauseAsync"/>
    public Task PauseAsync();

    /// <inheritdoc cref="IDebugSession.StepIntoAsync"/>
    public Task StepIntoAsync();

    /// <inheritdoc cref="IDebugSession.StepOverAsync"/>
    public Task StepOverAsync();

    /// <inheritdoc cref="IDebugSession.StepOutAsync"/>
    public Task StepOutAsync();

    /// <inheritdoc cref="IDebugSession.ReadMemoryAsync"/>
    public Task<string?> ReadMemoryAsync(string address, int byteCount);

    /// <inheritdoc cref="IDebugSession.SendRawCommandAsync"/>
    public Task SendRawCommandAsync(string command);
}
