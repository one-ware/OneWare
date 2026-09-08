using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Essentials.Debugger.Interfaces;

/// <summary>
/// Encapsulates one debug session and exposes <see cref="DebugSessionState"/> to the UI.
/// Backend syntax does not cross this interface (no GDB/MI).
/// <see cref="SendRawCommandAsync"/> is the one deliberate exception — it backs the console's
/// command line. Control commands return no result: what the target did afterwards arrives
/// through <see cref="StateChanged"/>, which is also how an unsolicited halt (e.g. a breakpoint
/// being hit) reaches the panels.
/// </summary>
public interface IDebugSession
{
    /// <summary>
    /// Identifies the backend, e.g. <c>GDB</c>.
    /// </summary>
    public string BackendId { get; }

    /// <summary>
    /// Latest published state.
    /// </summary>
    public DebugSessionState State { get; }

    /// <summary>
    /// Fired whenever <see cref="State"/> is replaced. May arrive on any thread.
    /// </summary>
    public event EventHandler<DebugSessionState>? StateChanged;

    /// <summary>
    /// Output of the debugged program, and readable messages from the backend.
    /// </summary>
    public event EventHandler<string>? OutputReceived;

    /// <summary>
    /// Every command sent to the backend, so the console can echo it.
    /// </summary>
    public event EventHandler<string>? CommandSent;

    /// <summary>
    /// The backend process ended, whether asked to or not.
    /// </summary>
    public event EventHandler? Exited;

    /// <summary>
    /// Brings the backend up and, for a remote request, attaches to the stub.
    /// Returns <see langword="false"/> if it did not come up and the session is unusable.
    /// </summary>
    public Task<bool> StartAsync();

    /// <summary>
    /// Starts the program. Separate from <see cref="ContinueAsync"/> — an attached target is
    /// already loaded and only needs resuming.
    /// </summary>
    public Task RunAsync();

    /// <summary>
    /// Resumes the halted target.
    /// </summary>
    public Task ContinueAsync();

    /// <summary>
    /// Halts the running target.
    /// </summary>
    public Task PauseAsync();

    /// <summary>
    /// Steps one source line, entering called functions.
    /// </summary>
    public Task StepIntoAsync();

    /// <summary>
    /// Steps one source line, stepping over called functions.
    /// </summary>
    public Task StepOverAsync();

    /// <summary>
    /// Runs until the current function returns.
    /// </summary>
    public Task StepOutAsync();

    /// <summary>
    /// Arms a breakpoint on the target. Returns <see langword="false"/> if the target refused it.
    /// A backend reports only <em>that</em> a breakpoint was refused, never <em>why</em>, so a
    /// caller cannot tell a target that is merely full from one that will never accept this
    /// breakpoint — which is what <see cref="DebugTargetProfile.MaxBreakpoints"/> is for.
    /// </summary>
    public Task<bool> SetBreakpointAsync(BreakPoint breakpoint);

    /// <summary>
    /// Removes a previously armed breakpoint. Removing one that is not armed counts as success —
    /// the requested state is what matters, not how it was reached.
    /// </summary>
    public Task<bool> RemoveBreakpointAsync(BreakPoint breakpoint);

    /// <summary>
    /// Reads memory from the target. <paramref name="address"/> is whatever the backend accepts —
    /// a literal such as <c>0x2001ff80</c>, or an expression like <c>&amp;buffer</c> when symbols
    /// exist. Returns <see langword="null"/> if the memory could not be read; a running target
    /// cannot be read, so call only while halted.
    /// </summary>
    public Task<string?> ReadMemoryAsync(string address, int byteCount);

    /// <summary>
    /// Full paths of every source file that contributed to the loaded program, so a caller can
    /// tell a breakpoint that belongs to this program from one that only happens to share a
    /// running debug session (the breakpoint store is shared across every open file, not scoped
    /// to one program). Returns <see langword="null"/> if the backend cannot answer — callers
    /// must treat that as "unknown", not as "no files", and skip filtering rather than reject
    /// every breakpoint.
    /// </summary>
    public Task<IReadOnlyList<string>?> GetSourceFilesAsync();

    /// <summary>
    /// Sends a command verbatim to the backend. The response arrives through
    /// <see cref="OutputReceived"/>, like any other backend output.
    /// </summary>
    public Task SendRawCommandAsync(string command);

    /// <summary>
    /// Tears the backend down. Synchronous and best-effort — also runs on application shutdown,
    /// where there is nothing left to await on.
    /// </summary>
    public void Stop();
}