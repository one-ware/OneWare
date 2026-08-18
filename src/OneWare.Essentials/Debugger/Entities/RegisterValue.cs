namespace OneWare.Essentials.Debugger.Entities;

// A single register as read from the target.
public sealed record RegisterValue(
    string Name, // as the target reports it, e.g. sp or pc
    string Value); // formatted by the backend, the UI displays the string unchanged
