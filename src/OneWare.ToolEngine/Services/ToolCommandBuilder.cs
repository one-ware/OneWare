using System.Text.RegularExpressions;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;

namespace OneWare.ToolEngine.Services;

public class ToolCommandBuilder : IToolCommandBuilder
{
    private readonly List<ICommandArgument> _args = new();
    private readonly Dictionary<string, string> _envVars = new();
    private readonly Dictionary<string, string> _strategyConfigurationOverrides = new();
    private readonly List<ToolPort> _exposedPorts = new();
    private readonly List<ToolPortMapping> _portMappings = new();
    private readonly string _toolName;
    private Func<string, bool>? _errorHandler;
    private string? _executable;
    private Func<string, bool>? _outputHandler;
    private bool _showTimer;
    private AppState _state = AppState.Loading;
    private string _status = "Running...";
    private string _workingDir = ".";
    private string? _forcedStrategyKey;

    internal ToolCommandBuilder(string toolName)
    {
        _toolName = toolName;
    }

    public IToolCommandBuilder WithExecutable(string path)
    {
        _executable = path;
        return this;
    }

    public IToolCommandBuilder AddRange(IEnumerable<string> literals)
    {
        foreach (var lit in literals) Add(lit);
        return this;
    }

    public IToolCommandBuilder AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths) AddPath(path);
        return this;
    }

    public IToolCommandBuilder AddIf(bool condition, string literal)
    {
        if (condition) Add(literal);
        return this;
    }

    public IToolCommandBuilder AddIfNotNull(string? literal)
    {
        if (!string.IsNullOrWhiteSpace(literal)) Add(literal);
        return this;
    }

    public IToolCommandBuilder AddOption(string flag, string value)
    {
        Add(flag);
        return Add(value);
    }

    public IToolCommandBuilder AddPathOption(string flag, string path)
    {
        Add(flag);
        return AddPath(path);
    }

    public IToolCommandBuilder Add(string literal)
    {
        _args.Add(new CommandArgument(literal));
        return this;
    }

    public IToolCommandBuilder Add(params string[] literals)
    {
        foreach (var lit in literals) Add(lit);
        return this;
    }

    public IToolCommandBuilder AddPath(string path)
    {
        _args.Add(new PathArgument(path));
        return this;
    }

    public IToolCommandBuilder AddScript(string template, params (string placeholder, string value)[] literals)
    {
        var mappings = literals.Select(x => (x.placeholder, x.value, isPath: false)).ToArray();
        _args.Add(new TemplateArgument(template, mappings));
        return this;
    }

    public IToolCommandBuilder AddScript(string template,
        params (string placeholder, string value, bool isPath)[] mappings)
    {
        _args.Add(new TemplateArgument(template, mappings));
        return this;
    }

    public IToolCommandBuilder WithWorkingDirectory(string dir)
    {
        _workingDir = dir;
        return this;
    }

    public IToolCommandBuilder WithStatus(string status, AppState state = AppState.Loading)
    {
        _status = status;
        _state = state;
        return this;
    }

    public IToolCommandBuilder WithTimer(bool show)
    {
        _showTimer = show;
        return this;
    }

    public IToolCommandBuilder WithOutputHandler(Func<string, bool>? handler)
    {
        _outputHandler = handler;
        return this;
    }

    public IToolCommandBuilder WithErrorHandler(Func<string, bool>? handler)
    {
        _errorHandler = handler;
        return this;
    }

    public IToolCommandBuilder AddRawArguments(string? rawArgs)
    {
        if (string.IsNullOrWhiteSpace(rawArgs)) return this;

        var parts = Regex
            .Matches(rawArgs, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value.Trim('"'));

        return AddRange(parts);
    }

    public IToolCommandBuilder AddOptionIfNotNull(string flag, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return this;

        Add(flag);
        Add(value);

        return this;
    }

    public IToolCommandBuilder AddPathOptionIfNotNull(string flag, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return this;

        Add(flag);
        AddPath(path);

        return this;
    }

    public IToolCommandBuilder AddPathFromMap<TKey>(TKey key, IDictionary<TKey, string> map) where TKey : notnull
    {
        if (map.TryGetValue(key, out var path)) AddPath(path);
        return this;
    }

    public IToolCommandBuilder WithEnvironmentVariable(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key)) _envVars[key] = value;
        return this;
    }

    public IToolCommandBuilder WithEnvironmentVariables(IDictionary<string, string> variables)
    {
        foreach (var kvp in variables) WithEnvironmentVariable(kvp.Key, kvp.Value);
        return this;
    }

    public IToolCommandBuilder WithEnvironmentVariableIf(bool condition, string key, string value)
    {
        return condition ? WithEnvironmentVariable(key, value) : this;
    }

    public IToolCommandBuilder WithExposedPort(int port, string protocol = "TCP")
    {
        if (!_exposedPorts.Any(p => p.Number == port && p.Protocol == protocol))
            _exposedPorts.Add(new ToolPort(port, protocol.ToUpper()));
        return this;
    }

    public IToolCommandBuilder AddPortMapping(int hostPort, int guestPort, string protocol = "TCP")
    {
        _portMappings.Add(new ToolPortMapping(
            new ToolPort(hostPort, protocol.ToUpper()),
            new ToolPort(guestPort, protocol.ToUpper())));
        WithExposedPort(guestPort, protocol);

        return this;
    }

    public IToolCommandBuilder ForceStrategy(string strategyKey)
    {
        _forcedStrategyKey = strategyKey;
        return this;
    }

    public IToolCommandBuilder WithStrategyConfiguration(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key)) _strategyConfigurationOverrides[key] = value;
        return this;
    }

    public ToolCommand Build()
    {
        if (string.IsNullOrWhiteSpace(_toolName) && string.IsNullOrWhiteSpace(_executable))
            throw new InvalidOperationException("Tool name or executable must be set.");

        return new ToolCommand
        {
            ToolName = _toolName,
            Executable = _executable ?? _toolName,
            CommandArguments = _args.AsReadOnly(),
            WorkingDirectory = _workingDir,
            StatusMessage = _status,
            State = _state,
            ShowTimer = _showTimer,
            OutputHandler = _outputHandler,
            ErrorHandler = _errorHandler,
            EnvironmentVariables = new Dictionary<string, string>(_envVars),
            PortMappings = _portMappings,
            ExposedPorts = _exposedPorts,
            ForcedStrategyKey = _forcedStrategyKey,
            StrategyConfigurationOverrides = new Dictionary<string, string>(_strategyConfigurationOverrides)
        };
    }
}