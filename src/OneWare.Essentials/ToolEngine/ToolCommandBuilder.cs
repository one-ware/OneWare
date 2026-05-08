using OneWare.Essentials.Enums;
using OneWare.Essentials.ToolEngine;

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

    public ToolCommandBuilder Add(string literal)
    {
        _args.Add(new CommandArgument(literal));
        return this;
    }

    public ToolCommandBuilder AddPath(string path)
    {
        _args.Add(new PathArgument(path));
        return this;
    }
    
    public ToolCommandBuilder AddScript(string template, params (string placeholder, string path)[] mappings)
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

    public ToolCommand Build()
    {
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