using OneWare.Essentials.Enums;

namespace OneWare.Essentials.ToolEngine;

public class ToolCommandBuilder(string toolName)
{
    private string? _executable;
    private readonly List<ICommandArgument> _args = new();
    private string _workingDir = ".";
    private string _status = "Running...";
    private AppState _state = AppState.Loading;
    private bool _showTimer = false;
    private Func<string, bool>? _outputHandler;
    private Func<string, bool>? _errorHandler;

    public ToolCommandBuilder WithExecutable(string path)
    {
        _executable = path;
        return this;
    }

    public ToolCommandBuilder AddRange(IEnumerable<string> literals)
    {
        foreach (var lit in literals) Add(lit);
        return this;
    }

    public ToolCommandBuilder AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths) AddPath(path);
        return this;
    }
    
    public ToolCommandBuilder AddIf(bool condition, string literal)
    {
        if (condition) Add(literal);
        return this;
    }

    public ToolCommandBuilder AddIfNotNull(string? literal)
    {
        if (!string.IsNullOrWhiteSpace(literal)) Add(literal);
        return this;
    }
    
    public ToolCommandBuilder AddOption(string flag, string value)
    {
        Add(flag);
        return Add(value);
    }

    public ToolCommandBuilder AddPathOption(string flag, string path)
    {
        Add(flag);
        return AddPath(path);
    }
    
    public ToolCommandBuilder Add(string literal)
    {
        _args.Add(new CommandArgument(literal));
        return this;
    }
    
    public ToolCommandBuilder Add(params string[] literals)
    {
        foreach (var lit in literals) Add(lit);
        return this;
    }

    public ToolCommandBuilder AddPath(string path)
    {
        _args.Add(new PathArgument(path));
        return this;
    }
    
    public ToolCommandBuilder AddScript(string template, params (string placeholder, string value)[] literals)
    {
        var mappings = literals.Select(x => (x.placeholder, x.value, isPath: false)).ToArray();
        _args.Add(new TemplateArgument(template, mappings));
        return this;
    }

    public ToolCommandBuilder AddScript(string template, params (string placeholder, string value, bool isPath)[] mappings)
    {
        _args.Add(new TemplateArgument(template, mappings));
        return this;
    }
    
    public ToolCommandBuilder WithWorkingDirectory(string dir)
    {
        _workingDir = dir;
        return this;
    }

    public ToolCommandBuilder WithStatus(string status, AppState state = AppState.Loading)
    {
        _status = status;
        _state = state;
        return this;
    }

    public ToolCommandBuilder WithTimer(bool show)
    {
        _showTimer = show;
        return this;
    }

    public ToolCommandBuilder WithOutputHandler(Func<string, bool> handler)
    {
        _outputHandler = handler;
        return this;
    }

    public ToolCommandBuilder WithErrorHandler(Func<string, bool> handler)
    {
        _errorHandler = handler;
        return this;
    }

    public ToolCommandBuilder AddRawArguments(string? rawArgs)
    {
        if (string.IsNullOrWhiteSpace(rawArgs)) return this;
        
        var parts = System.Text.RegularExpressions.Regex
            .Matches(rawArgs, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value.Trim('"'));

        return AddRange(parts);
    }
    
    public ToolCommand Build()
    {
        if (string.IsNullOrWhiteSpace(toolName) && string.IsNullOrWhiteSpace(_executable))
        {
            throw new InvalidOperationException("Tool name or executable must be set.");
        }
        
        return new ToolCommand
        {
            ToolName = toolName,
            Executable = _executable ?? toolName,
            CommandArguments = _args.AsReadOnly(),
            WorkingDirectory = _workingDir,
            StatusMessage = _status,
            State = _state,
            ShowTimer = _showTimer,
            OutputHandler = _outputHandler,
            ErrorHandler = _errorHandler
        };
    }
}