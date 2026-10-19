namespace OneWare.Essentials.Models;

public record TerminalExecutionResult(string Output, int ExitCode, bool TimedOut)
{
    /// <summary>
    /// The directory the shell was in after the command finished, or null when the shell does not report it
    /// (no shell integration).
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Number of characters the command printed, counting a line break as one character even though the terminal
    /// delivers it as "\r\n". Larger than <see cref="Output" /> when the middle of a very long output was omitted;
    /// 0 when unknown.
    /// </summary>
    public long OutputLength { get; init; }

    /// <summary>
    /// Number of lines the command printed (an unterminated last line counts), including omitted ones; 0 when
    /// unknown.
    /// </summary>
    public long OutputLineCount { get; init; }
}
