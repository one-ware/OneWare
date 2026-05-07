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

    public void ConvertFilePaths(OSPlatform osPlatform)
    {
        foreach (var argument in CommandArguments)
        {
            if (argument is PathArgument pathArgument)
            {
                pathArgument.ChangeOsPath(osPlatform);
            }
        }
    }

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
}

public interface ICommandArgument
{
    string GetArgument();
}

public class CommandArgument(string argument) : ICommandArgument
{
    public string GetArgument()
    {
        return argument;
    }
}

public class PathArgument(string path) : ICommandArgument
{
    private string _path = path;

    public void ChangeOsPath(OSPlatform osPlatform)
    {
        if (osPlatform == OSPlatform.Windows)
        {
            _path = _path.Replace("/", "\\");
        }
        else
        {
            _path = _path.Replace("\\", "/");
        }
    }
    
    public string GetArgument()
    {
        return _path;
    }
}

public class ToolContext
{
    public ToolContext(string name, string description, string key, List<string>? toolNames = null)
    {
        Name = name;
        Description = description;
        Key = key;
        ToolNames = toolNames ?? [];
    }

    public string Name { get; init; }
    public string Description { get; init; }
    public string Key { get; init; }

    public List<string> ToolNames { get; init; }
}

public class ToolConfiguration
{
    public readonly Dictionary<string, string> StrategyMapping = new();
}