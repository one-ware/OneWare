namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// A local variable of the frame the target is halted in.
/// </summary>
/// <param name="Name">As it appears in the source.</param>
/// <param name="Value">Formatted by the backend; the UI displays the string unchanged.</param>
/// <param name="TypeName">Declared type, or <see langword="null"/> if the backend did not report one.</param>
public sealed record DebugVariable(
    string Name,
    string Value,
    string? TypeName);
