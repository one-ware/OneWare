namespace OneWare.Essentials.Debugger.Entities;

// A local variable of the frame the target is halted in.
public sealed record DebugVariable(
    string Name, // as it appears in the source
    string Value, // formatted by the backend, the UI displays the string unchanged
    string? TypeName); // declared type, null if the backend did not report one
