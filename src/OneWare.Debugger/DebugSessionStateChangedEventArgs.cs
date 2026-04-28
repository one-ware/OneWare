namespace OneWare.Debugger;

public sealed class DebugSessionStateChangedEventArgs(DebugSessionState state) : EventArgs
{
    public DebugSessionState State { get; } = state;
}
