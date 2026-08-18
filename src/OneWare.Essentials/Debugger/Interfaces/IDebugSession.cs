using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Essentials.Debugger.Interfaces;

// Heavyweight which capsules the debug session and exposes DebugSessionState to the UI
// with one important rule: backend syntax does not cross this interface (no GDB/MI).
// SendRawCommandAsync is the one deliberate exception -> it backs the console's command line.
// The control commands return no result: what the target did afterwards arrives through
// StateChanged, which is also how a halt nobody asked for - a breakpoint being hit - reaches
// the panels.
public interface IDebugSession
{
    // Backend ID e.g. GDB
    public string AdapterId { get; }

    // Latest published state.
    public DebugSessionState State { get; }

    // Fired whenever a State is replaced. May arrive on any thread.
    public event EventHandler<DebugSessionState>? StateChanged;

    // Output of the debugged program, and readable messages from the backend.
    public event EventHandler<string>? OutputReceived;

    // Every command sent to the backend, so the console can echo it.
    public event EventHandler<string>? CommandSent;

    // The backend process ended, whether asked to or not.
    public event EventHandler? Exited;

    // Brings the backend up and, for a remote request, attaches to the stub
    // -> false means it did not come up and the session is unusable
    public Task<bool> StartAsync();

    // Starts the program. Separate from ContinueAsync -> an attached target is already loaded
    // and only needs resuming
    public Task RunAsync();

    // Resumes the halted target
    public Task ContinueAsync();

    // Halts the running target
    public Task PauseAsync();

    // One source line, entering called functions
    public Task StepIntoAsync();

    // One source line, stepping over called functions
    public Task StepOverAsync();

    // Runs until the current function returns
    public Task StepOutAsync();

    // Arms a breakpoint on the target -> false if the target refused it, for instance because it
    // ran out of hardware breakpoints
    public Task<bool> SetBreakpointAsync(BreakPoint breakpoint);

    // Removes a previously armed breakpoint
    public Task<bool> RemoveBreakpointAsync(BreakPoint breakpoint);

    // address is whatever the backend accepts - a literal such as 0x2001ff80, or an expression
    // like &buffer when symbols exist. Returns null if it could not be read; a running target
    // cannot be read at all, so ask only while halted.
    public Task<string?> ReadMemoryAsync(string address, int byteCount);

    // Sends a command verbatim to the backend. The response arrives through OutputReceived,
    // like any other backend output.
    public Task SendRawCommandAsync(string command);

    // Tears the backend down. Synchronous and best effort -> it also runs on application
    // shutdown, where there is nothing left to await on.
    public void Stop();
}
