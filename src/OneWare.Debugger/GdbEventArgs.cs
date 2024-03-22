namespace OneWare.Debugger;

public class GdbEventArgs(GdbEvent gdbEvent)
{
    public GdbEvent GdbEvent { get; } = gdbEvent;
}