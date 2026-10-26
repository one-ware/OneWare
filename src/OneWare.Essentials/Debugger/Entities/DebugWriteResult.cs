namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// Outcome of writing a value to the target. A failure carries the backend's own wording —
/// only the backend knows why a value was refused, and the panel shows it next to the value
/// the user typed.
/// </summary>
/// <param name="Succeeded">Whether the target took the value.</param>
/// <param name="Message">Why it did not, in the backend's words; <see langword="null"/> on success.</param>
public sealed record DebugWriteResult(
    bool Succeeded,
    string? Message)
{
    /// <summary>
    /// The value was written.
    /// </summary>
    public static DebugWriteResult Ok { get; } = new(true, null);

    /// <summary>
    /// The value was not written, for the given reason.
    /// </summary>
    public static DebugWriteResult Failed(string message)
    {
        return new DebugWriteResult(false, message);
    }
}
