namespace OneWare.Debugger;

public sealed class DebugStackFrame
{
    public required int Level { get; init; }
    public string? Address { get; init; }
    public string? Function { get; init; }
    public string? FileName { get; init; }
    public string? FullPath { get; init; }
    public int Line { get; init; }
}
