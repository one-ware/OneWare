using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Essentials.Debugger.Interfaces;

// The service a plugin resolves in order to take part in debugging. The dependency runs one way
// only -> plugins depend on these contracts, the core never learns that a given plugin exists.
public interface IDebuggerService
{
    // Registered backends, the core's own included
    public IReadOnlyList<IDebugAdapter> Adapters { get; }

    // preparation step: whoever fits the active project shows up in the launch selection
    public IReadOnlyList<IDebugLaunchProvider> LaunchProviders { get; }

    // The active session, or null if none is running
    public IDebugSession? CurrentSession { get; }

    // State of the active session, or DebugSessionState.Empty if none is running
    public DebugSessionState State { get; }

    // Whether a session is running -> this is what gates the breakpoint margin in the editor
    public bool IsActive { get; }

    // Fired when State, CurrentSession or IsActive changed. Always raised on the UI thread,
    // so handlers can touch bound collections directly.
    public event EventHandler? StateChanged;

    // Resolved from the container -> the implementation gets constructor injection like any
    // other service
    public void RegisterAdapter<T>() where T : IDebugAdapter;

    // preparation step: registration works like the adapters, resolved through the container
    public void RegisterLaunchProvider<T>() where T : IDebugLaunchProvider;

    // Starts a session, arms the breakpoints currently set in the editor and runs the program
    // -> false if no adapter accepted the request or the backend did not come up; nothing is
    // left running in that case
    public Task<bool> StartAsync(DebugLaunchRequest launchRequest);

    // preparation step: PrepareAsync first, then start with its request;
    // CleanupAsync runs as soon as the session ends - no matter how it ended
    public Task<bool> StartAsync(IDebugLaunchProvider provider, CancellationToken ct = default);

    // Ends the active session. Does nothing if none is running.
    public Task StopAsync();

    // The commands below mirror IDebugSession and forward to CurrentSession, doing nothing when
    // there is none -> panels bind to this service alone and never have to rebind when a session
    // starts or ends
    public Task ContinueAsync();

    public Task PauseAsync();

    public Task StepIntoAsync();

    public Task StepOverAsync();

    public Task StepOutAsync();

    public Task<string?> ReadMemoryAsync(string address, int byteCount);

    public Task SendRawCommandAsync(string command);
}
