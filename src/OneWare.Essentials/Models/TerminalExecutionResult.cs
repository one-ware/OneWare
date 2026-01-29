namespace OneWare.Essentials.Models;

public record TerminalExecutionResult(string Output, int ExitCode, bool TimedOut);