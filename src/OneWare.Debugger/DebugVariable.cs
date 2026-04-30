namespace OneWare.Debugger;

public sealed class DebugVariable
{
    public required string Name { get; init; }
    public string? Value { get; init; }
    public string? TypeName { get; init; }
    public IReadOnlyList<DebugVariable> Children { get; init; } = Array.Empty<DebugVariable>();
}
