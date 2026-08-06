namespace OneWare.Essentials.Debugger;

/// <summary>
/// Everything the user interface knows about the target at one point in time.
/// </summary>
/// <remarks>
/// A snapshot rather than a lifecycle enum plus a pile of separate events. The session publishes a
/// complete replacement on every change, so a panel binds one thing and cannot end up showing
/// registers from before the last step next to a frame from after it.
/// </remarks>
public sealed record DebugSessionState
{
    /// <summary>
    /// No session, or a session that has ended. Also the state a session starts out in.
    /// </summary>
    public static DebugSessionState Empty { get; } = new();

    /// <summary>
    /// The target is executing. Nothing can be inspected and only pausing is meaningful.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Where the target is halted, or <c>null</c> while it runs.
    /// </summary>
    public DebugStackFrame? CurrentFrame { get; init; }

    /// <summary>
    /// Register contents as of the last halt. Empty while the target runs, and empty for a backend
    /// that cannot read registers — the panel then simply shows nothing, which is what a separate
    /// capability flag would have told it to do anyway.
    /// </summary>
    public IReadOnlyList<RegisterValue> Registers { get; init; } = [];

    /// <summary>
    /// Local variables of <see cref="CurrentFrame"/> as of the last halt. Empty while the target
    /// runs, and empty without debug symbols — a target whose program was never linked with them
    /// has no names to report, only registers.
    /// </summary>
    public IReadOnlyList<DebugVariable> Locals { get; init; } = [];
}
