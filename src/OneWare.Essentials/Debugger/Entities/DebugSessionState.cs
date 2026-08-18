namespace OneWare.Essentials.Debugger.Entities;

// Everything the UI knows about session and target at one point in time.
// A snapshot rather than a lifecycle enum plus a pile of separate events -> the session publishes
// a complete replacement on every change, so a panel binds one thing and cannot end up showing
// registers from before the last step next to a frame from after it.
public sealed record DebugSessionState
{
    // No session, or a session that has ended. Also the state a session starts out in.
    public static DebugSessionState Empty { get; } = new();

    // The target is executing -> nothing can be inspected and only pausing is meaningful
    public bool IsRunning { get; init; }

    // Where the target is halted, or null while it runs
    public DebugStackFrame? CurrentFrame { get; init; }

    // Register contents as of the last halt. Empty while the target runs, and empty for a backend
    // that cannot read registers -> the panel then simply shows nothing, which is what a separate
    // capability flag would have told it to do anyway.
    public IReadOnlyList<RegisterValue> Registers { get; init; } = [];

    // Locals of CurrentFrame as of the last halt. Empty while the target runs, and empty without
    // debug symbols -> a program that was never linked with them has no names to report, only
    // registers.
    public IReadOnlyList<DebugVariable> Locals { get; init; } = [];
}
