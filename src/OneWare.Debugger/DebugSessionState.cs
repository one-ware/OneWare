namespace OneWare.Debugger;

public sealed class DebugSessionState
{
    public static DebugSessionState Empty { get; } = new();

    public bool IsRunning { get; init; }
    public DebugStackFrame? CurrentFrame { get; init; }
    public IReadOnlyList<DebugStackFrame> CallStack { get; init; } = Array.Empty<DebugStackFrame>();
    public IReadOnlyList<DebugVariable> Locals { get; init; } = Array.Empty<DebugVariable>();
}
