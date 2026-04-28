using OneWare.Essentials.EditorExtensions;

namespace OneWare.Debugger;

/// <summary>
/// High-level service that owns at most one active <see cref="GdbSession"/>
/// and routes its output, breakpoints and lifecycle to the rest of the IDE.
/// </summary>
public interface IDebuggerService
{
    /// <summary>
    /// True while a GDB session is active (started and not yet exited).
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// The currently active session, or <c>null</c> if no debugging is in progress.
    /// </summary>
    GdbSession? CurrentSession { get; }

    /// <summary>
    /// The shared breakpoint store that all editors and the active GDB session use.
    /// </summary>
    BreakpointStore Breakpoints { get; }

    /// <summary>
    /// Raised when a session starts, stops or its running state changes.
    /// Listeners can use this to refresh debug-related UI.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Starts a new GDB session for the given executable. If a session is already
    /// running it is stopped first. Returns true on success.
    /// </summary>
    Task<bool> StartAsync(string executable);

    /// <summary>
    /// Stops the active session, if any.
    /// </summary>
    void Stop();

    /// <summary>
    /// Continue execution. No-op if no active session.
    /// </summary>
    void Continue();

    /// <summary>
    /// Pause execution. No-op if no active session.
    /// </summary>
    void Pause();

    /// <summary>
    /// Step into the next instruction.
    /// </summary>
    void Step();

    /// <summary>
    /// Step over the current line.
    /// </summary>
    void Next();

    /// <summary>
    /// Step out of the current frame.
    /// </summary>
    void Finish();
}
