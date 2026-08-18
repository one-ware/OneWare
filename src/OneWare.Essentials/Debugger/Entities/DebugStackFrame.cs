namespace OneWare.Essentials.Debugger.Entities;

// Where the target is halted.
public sealed record DebugStackFrame(
    string? Function, // name of the function, if the backend reported one
    string? File, // absolute source path, null if the address could not be mapped -> the editor only jumps when this is set
    int Line, // one based, 0 if unknown
    string? Address); // program counter as the backend formatted it, e.g. 0x00000108 -> the only location without symbols
