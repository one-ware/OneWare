namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// A place in the target. Used in both directions: the session reports where the target is
/// halted, and a breakpoint asks it to halt somewhere. Both answer the same question — where —
/// so both use the same type, and putting a breakpoint on the spot the target already stands on
/// needs no conversion.
/// Deliberately not the editor's own breakpoint model: while a published contract named that
/// class, every change to the margin was a change to published API.
/// </summary>
/// <param name="Function">Name of the function, if the backend reported one. May also be set on
/// its own to place a breakpoint on a function by name.</param>
/// <param name="File">
/// Absolute source path, or <see langword="null"/> if the address could not be mapped — a
/// program without debug symbols, code compiled without them, or a halt at an address the line
/// table does not cover. Set on every breakpoint coming from the margin.
/// The editor only jumps to the source location when this is set.
/// </param>
/// <param name="Line">One-based line number. <c>0</c> means "unknown" when read from the target
/// and "no line given" when written to it.</param>
/// <param name="Address">
/// Program counter as formatted by the backend, e.g. <c>0x00000108</c>. The only location
/// available when no debug symbols are present.
/// </param>
public sealed record DebugBreakPointFrame(  
    string? Function,
    string? File,
    int Line,
    string? Address);
