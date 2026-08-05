namespace OneWare.Essentials.Debugging;

/// <summary>
/// A single register as read from the target.
/// </summary>
/// <param name="Name">Register name as the target reports it, for example <c>sp</c> or <c>pc</c>.</param>
/// <param name="Value">Formatted value. Formatting is the backend's business; the user interface
/// displays the string unchanged.</param>
public sealed record RegisterValue(string Name, string Value);
