namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// Where the target is halted.
/// </summary>
/// <param name="Function">Name of the function, if the backend reported one.</param>
/// <param name="File">
/// Absolute source path, or <see langword="null"/> if the address could not be mapped.
/// The editor only jumps to the source location when this is set.
/// </param>
/// <param name="Line">One-based line number, or <c>0</c> if unknown.</param>
/// <param name="Address">
/// Program counter as formatted by the backend, e.g. <c>0x00000108</c>. The only location
/// available when no debug symbols are present.
/// </param>
public sealed record DebugStackFrame(
    string? Function,
    string? File,
    int Line,
    string? Address);
