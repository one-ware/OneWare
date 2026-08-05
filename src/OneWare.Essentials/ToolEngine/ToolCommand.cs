using System.Runtime.InteropServices;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.ToolEngine;

public class ToolCommand
{
    public required string ToolName { get; init; }
    public string? Executable { get; init; }
    
    public IReadOnlyCollection<ToolPort> ExposedPorts { get; init; } = Array.Empty<ToolPort>();
    
    public IReadOnlyCollection<ToolPortMapping> PortMappings { get; init; } = Array.Empty<ToolPortMapping>();
    
    public IReadOnlyCollection<string> Arguments => 
        CommandArguments.Select(x => x.GetArgument()).ToList().AsReadOnly();
    
    public required IReadOnlyCollection<ICommandArgument> CommandArguments { get; init; }

    /// <summary>
    /// When set, forces this specific call to run with the strategy matching this key, regardless of the
    /// tool's currently configured strategy setting. Fails (see <see cref="IToolExecutionDispatcherService"/>
    /// return conventions) if no such strategy is registered for the tool.
    /// </summary>
    public string? ForcedStrategyKey { get; init; }

    public string WorkingDirectory { get; init; } = ".";
    public string StatusMessage { get; init; } = "Running tool...";
    public AppState State { get; init; } = AppState.Loading;
    public bool ShowTimer { get; init; }

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Per-call overrides for strategy configuration keys (e.g. "docker.image"), taking precedence over
    /// both the tool's plugin-declared default and any user-set override for this call only. Retrieve the
    /// fully merged result via <see cref="IToolService.GetEffectiveStrategyConfiguration"/> rather than
    /// reading this property directly.
    /// </summary>
    public IReadOnlyDictionary<string, string> StrategyConfigurationOverrides { get; init; } =
        new Dictionary<string, string>();

    public Func<string, bool>? OutputHandler { get; init; }
    public Func<string, bool>? ErrorHandler { get; init; }

    public void PrepareCommand(OSPlatform osPlatform, Func<string, string>? pathMapper = null)
    {
        foreach (var argument in CommandArguments)
        {
            argument.Prepare(osPlatform, pathMapper);
        }
    }
    [Obsolete("Use IToolExecutionDispatcherService.CreateToolCommandBuilder instead.")]
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
        return ContainerLocator.Current.Resolve<IToolExecutionDispatcherService>().
            CreateToolCommandBuilder(Path.GetFileNameWithoutExtension(path))
            .WithExecutable(path)
            .WithWorkingDirectory(workingDirectory)
            .WithStatus(status, state)
            .WithOutputHandler(outputAction)
            .WithErrorHandler(errorAction)
            .WithTimer(showTimer)
            .AddRange(arguments)
            .Build();
    }
}

public record ToolPort(int Number, string Protocol = "TCP");

public record ToolPortMapping(ToolPort Host, ToolPort Guest);