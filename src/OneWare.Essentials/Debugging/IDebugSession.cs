using OneWare.Essentials.EditorExtensions;

namespace OneWare.Essentials.Debugging;

/// <summary>
/// A running, controllable debug session.
/// </summary>
/// <remarks>
/// Backend syntax does not cross this interface — no GDB/MI, no <c>-exec-*</c>. Translating
/// <see cref="StepOverAsync"/> into whatever the backend speaks is the implementation's job. The
/// one deliberate exception is <see cref="SendRawCommandAsync"/>, which backs the free-form command
/// line of the debugger console.
/// <para>
/// Every control command returns a <see cref="Task"/> because the backend is a process this has to
/// wait on. None of them return the result: what the target did afterwards arrives through
/// <see cref="StateChanged"/>, which is also how a halt the user did not ask for — a breakpoint
/// being hit — reaches the panels.
/// </para>
/// </remarks>
public interface IDebugSession
{
    /// <summary>
    /// <see cref="IDebugAdapter.Id"/> of the backend behind this session.
    /// </summary>
    public string AdapterId { get; }

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
    /// </summary>
    /// <returns><c>false</c> if the backend did not come up; the session is then unusable.</returns>
    public Task<bool> StartAsync();

    /// <summary>
    /// Starts the program running. Separate from <see cref="ContinueAsync"/> because an attached
    /// target is already loaded and only needs resuming.
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
    /// Executes one source line, entering called functions.
    /// </summary>
    public Task StepIntoAsync();

    /// <summary>
    /// Executes one source line, stepping over called functions.
    /// </summary>
    public Task StepOverAsync();

    /// <summary>
    /// Runs until the current function returns.
    /// </summary>
    public Task StepOutAsync();

    /// <summary>
    /// Arms a breakpoint on the target.
    /// </summary>
    /// <returns><c>false</c> if the target refused it, for instance because it ran out of hardware
    /// breakpoints.</returns>
    public Task<bool> SetBreakpointAsync(BreakPoint breakpoint);

    /// <summary>
    /// Removes a previously armed breakpoint.
    /// </summary>
    public Task<bool> RemoveBreakpointAsync(BreakPoint breakpoint);

    /// <summary>
    /// Reads target memory.
    /// </summary>
    /// <param name="address">Where to read, in whatever the backend accepts — a literal address
    /// such as <c>0x2001ff80</c>, or an expression like <c>&amp;buffer</c> when symbols exist.</param>
    /// <param name="byteCount">How many bytes to read.</param>
    /// <returns>The bytes formatted for display, or <c>null</c> if the address could not be read.
    /// A running target cannot be read at all — the caller is expected to ask only while halted.</returns>
    public Task<string?> ReadMemoryAsync(string address, int byteCount);

    /// <summary>
    /// Sends a command verbatim to the backend. The response arrives through
    /// <see cref="OutputReceived"/> like any other backend output.
    /// </summary>
    public Task SendRawCommandAsync(string command);

    /// <summary>
    /// Tears the backend down. Synchronous and best-effort: it is also what runs when the
    /// application is shutting down, where there is nothing left to await on.
    /// </summary>
    public void Stop();
}
