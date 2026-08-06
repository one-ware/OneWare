namespace OneWare.Essentials.Debugger;

/// <summary>
/// A local variable of the frame the target is halted in.
/// </summary>
/// <param name="Name">Variable name as it appears in the source.</param>
/// <param name="Value">Formatted value. Formatting is the backend's business; the user interface
/// displays the string unchanged.</param>
/// <param name="TypeName">Declared type, or <c>null</c> if the backend did not report one.</param>
public sealed record DebugVariable(string Name, string Value, string? TypeName);
