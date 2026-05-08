using System.Runtime.InteropServices;
using OneWare.Essentials.Enums;

namespace OneWare.Essentials.ToolEngine;

public class ToolCommand
{
    public required string ToolName { get; init; }
    public string? Executable { get; init; }
    public IReadOnlyCollection<string> Arguments => 
        CommandArguments.Select(x => x.GetArgument()).ToList().AsReadOnly();
    
    public IReadOnlyCollection<ICommandArgument> CommandArguments { get; init; } = [];
    public string WorkingDirectory { get; init; } = ".";
    public string StatusMessage { get; init; } = "Running tool...";
    public AppState State { get; init; } = AppState.Loading;
    public bool ShowTimer { get; init; }

    public Func<string, bool>? OutputHandler { get; init; }
    public Func<string, bool>? ErrorHandler { get; init; }

    private static IReadOnlyCollection<ICommandArgument> ParseArguments(IReadOnlyCollection<string> arguments)
    {
        return arguments.Select(arg => new CommandArgument(arg)).Cast<ICommandArgument>().ToList();
    }
    
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
            CommandArguments = ParseArguments(arguments),
            WorkingDirectory = workingDirectory,
            StatusMessage = status,
            State = state,
            ShowTimer = showTimer,
            OutputHandler = outputAction,
            ErrorHandler = errorAction
        };
    }
    
    public static ToolCommand FromParams(
        string path,
        IReadOnlyCollection<ICommandArgument> arguments,
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
            CommandArguments = arguments,
            WorkingDirectory = workingDirectory,
            StatusMessage = status,
            State = state,
            ShowTimer = showTimer,
            OutputHandler = outputAction,
            ErrorHandler = errorAction
        };
    }
    
    public static ToolCommand FromWeakParams(
        string path,
        IReadOnlyCollection<string> arguments,
        string workingDirectory)
    {
        return new ToolCommand
        {
            ToolName = Path.GetFileNameWithoutExtension(path),
            Executable = path,
            CommandArguments = ParseArguments(arguments),
            WorkingDirectory = workingDirectory
        };
    }
    
    public void PrepareCommand(OSPlatform osPlatform, Func<string, string>? pathMapper = null)
    {
        foreach (var argument in CommandArguments)
        {
            argument.Prepare(osPlatform, pathMapper);
        }
    }
}
