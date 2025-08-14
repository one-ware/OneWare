using OneWare.Essentials.Enums;

namespace OneWare.Essentials.Models;

public class ToolCommand
{
    public string ToolName { get; init; }           // z.B. "yosys"
    public string Executable { get; init; }            // z.B. "yosys"
    public IReadOnlyCollection<string> Arguments { get; init; } = Array.Empty<string>();
    public string WorkingDirectory { get; init; } = ".";
    public string StatusMessage { get; init; } = "Running tool...";
    public AppState State { get; init; } = AppState.Loading;
    public bool ShowTimer { get; init; } = false;

    public Func<string, bool>? OutputHandler { get; init; }
    public Func<string, bool>? ErrorHandler { get; init; }
    
    public static ToolCommand FromShellParams(
        string path,
        IReadOnlyCollection<string> arguments,
        string workingDirectory,
        string status,
        AppState state = AppState.Loading,
        bool showTimer = false,
        Func<string, bool>? outputAction = null,
        Func<string, bool>? errorAction = null)
    {
        return new ToolCommand
        {
            ToolName = Path.GetFileNameWithoutExtension(path),
            Executable = path,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            StatusMessage = status,
            State = state,
            ShowTimer = showTimer,
            OutputHandler = outputAction,
            ErrorHandler = errorAction
        };
    }
}