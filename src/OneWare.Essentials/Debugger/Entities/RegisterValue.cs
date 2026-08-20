namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// A single register as read from the target.
/// </summary>
/// <param name="Name">As reported by the target, e.g. <c>sp</c> or <c>pc</c>.</param>
/// <param name="Value">Formatted by the backend; the UI displays the string unchanged.</param>
public sealed record RegisterValue(
    string Name,
    string Value);
