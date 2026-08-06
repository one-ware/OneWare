namespace OneWare.Essentials.Debugger;

/// <summary>
/// Where the target is halted.
/// </summary>
/// <param name="Function">Name of the function, if the backend reported one.</param>
/// <param name="File">Absolute path of the source file, or <c>null</c> if the backend could not map
/// the address to a file. The editor only jumps to a line if this is set.</param>
/// <param name="Line">One-based source line, or <c>0</c> if unknown.</param>
/// <param name="Address">Program counter as the backend formatted it, for example <c>0x00000108</c>.
/// The only location information available when debugging without symbols.</param>
public sealed record DebugStackFrame(string? Function, string? File, int Line, string? Address);
