using OneWare.Essentials.Enums;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

/// <summary>
/// A fluent builder for creating <see cref="ToolCommand"/> instances.
/// Supports cross-platform path handling, scripting placeholders, and container/networking configurations.
/// </summary>
public interface IToolCommandBuilder
{
    /// <summary>
    /// Sets the path to the executable file. 
    /// If not explicitly set, the ToolName provided during initialization will be used as the executable.
    /// </summary>
    IToolCommandBuilder WithExecutable(string path);
    
    /// <summary>
    /// Sets the working directory for the tool execution. Defaults to "."
    /// </summary>
    IToolCommandBuilder WithWorkingDirectory(string dir);
    
    /// <summary>
    /// Defines the status message and application state to be displayed in the UI during execution.
    /// </summary>
    IToolCommandBuilder WithStatus(string status, AppState state = AppState.Loading);
    
    /// <summary>
    /// Determines whether a timer should be displayed in the UI during the tool's execution.
    /// </summary>
    IToolCommandBuilder WithTimer(bool show);
    
    /// <summary>
    /// Registers a handler for the standard output stream (stdout).
    /// </summary>
    IToolCommandBuilder WithOutputHandler(Func<string, bool>? handler);
    
    /// <summary>
    /// Registers a handler for the error output stream (stderr).
    /// </summary>
    IToolCommandBuilder WithErrorHandler(Func<string, bool>? handler);

    /// <summary>
    /// Adds a simple string literal as a command-line argument.
    /// </summary>
    IToolCommandBuilder Add(string literal);
    
    /// <summary>
    /// Adds multiple string literals as command-line arguments.
    /// </summary>
    IToolCommandBuilder Add(params string[] literals);
    
    /// <summary>
    /// Adds a collection of string literals as command-line arguments.
    /// </summary>
    IToolCommandBuilder AddRange(IEnumerable<string> literals);
    
    /// <summary>
    /// Adds an argument only if the specified condition is met.
    /// </summary>
    IToolCommandBuilder AddIf(bool condition, string literal);
    
    /// <summary>
    /// Adds an argument only if the provided string is not null or whitespace.
    /// </summary>
    IToolCommandBuilder AddIfNotNull(string? literal);

    /// <summary>
    /// Adds a file or directory path as an argument. 
    /// The path will be normalized according to the target OS (Windows/Linux) during preparation.
    /// </summary>
    IToolCommandBuilder AddPath(string path);
    
    /// <summary>
    /// Adds a collection of paths as arguments, ensuring OS-specific normalization for each.
    /// </summary>
    IToolCommandBuilder AddPaths(IEnumerable<string> paths);

    /// <summary>
    /// Adds an option consisting of a flag and a value (e.g., "-o", "output.bin").
    /// </summary>
    IToolCommandBuilder AddOption(string flag, string value);
    
    /// <summary>
    /// Adds an option consisting of a flag and a value only if the value is not null or whitespace.
    /// </summary>
    IToolCommandBuilder AddOptionIfNotNull(string flag, string? value);

    /// <summary>
    /// Adds an option consisting of a flag and a path. The path will be normalized.
    /// </summary> 
    IToolCommandBuilder AddPathOption(string flag, string path);
    
    /// <summary>
    /// Adds an option consisting of a flag and a path only if the path is not null or whitespace.
    /// </summary>
    IToolCommandBuilder AddPathOptionIfNotNull(string flag, string? path);
    
    /// <summary>
    /// Adds a complex command string using template placeholders. 
    /// Placeholders are treated as simple literals.
    /// </summary>
    IToolCommandBuilder AddScript(string template, params (string placeholder, string value)[] literals);
    
    /// <summary>
    /// Adds a complex command string using template placeholders. 
    /// Allows placeholders to be explicitly marked as paths for OS-specific normalization.
    /// </summary>
    IToolCommandBuilder AddScript(string template, params (string placeholder, string value, bool isPath)[] mappings);

    /// <summary>
    /// Parses a raw string (e.g., from user settings) into individual arguments, respecting quotes.
    /// </summary>
    IToolCommandBuilder AddRawArguments(string? rawArgs);
    
    /// <summary>
    /// Looks up a path in a dictionary by its key and adds it as a normalized path argument.
    /// </summary>
    IToolCommandBuilder AddPathFromMap<TKey>(TKey key, IDictionary<TKey, string> map) where TKey : notnull;

    /// <summary>
    /// Sets an environment variable for the tool process.
    /// </summary>
    IToolCommandBuilder WithEnvironmentVariable(string key, string value);
    
    /// <summary>
    /// Adds a collection of environment variables for the tool process.
    /// </summary>
    IToolCommandBuilder WithEnvironmentVariables(IDictionary<string, string> variables);
    
    /// <summary>
    /// Sets an environment variable only if the specified condition is met.
    /// </summary>
    IToolCommandBuilder WithEnvironmentVariableIf(bool condition, string key, string value);

    /// <summary>
    /// Declares a network port that the tool listens on internally. 
    /// Used for firewall configuration or container port exposure.
    /// </summary>
    IToolCommandBuilder WithExposedPort(int port, string protocol = "TCP");
    
    /// <summary>
    /// Defines a network port mapping.
    /// Native runners typically use the guestPort directly, while container runners map the hostPort to the guestPort.
    /// </summary>
    IToolCommandBuilder AddPortMapping(int hostPort, int guestPort, string protocol = "TCP");

    /// <summary>
    /// Forces this command to run with the strategy matching <paramref name="strategyKey"/>, regardless of
    /// the tool's currently configured strategy setting. Use when a specific call has requirements
    /// (e.g. container networking) that only one particular strategy can satisfy.
    /// </summary>
    IToolCommandBuilder ForceStrategy(string strategyKey);

    /// <summary>
    /// Overrides a strategy configuration key (e.g. "docker.image") for this call only, taking precedence
    /// over both the tool's plugin-declared default and any user-set override.
    /// </summary>
    IToolCommandBuilder WithStrategyConfiguration(string key, string value);

    /// <summary>
    /// Builds the final <see cref="ToolCommand"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if neither ToolName nor Executable is set.</exception>
    ToolCommand Build();
}