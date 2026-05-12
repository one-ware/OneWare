using OneWare.Essentials.Enums;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolCommandBuilder
{
    IToolCommandBuilder WithExecutable(string path);
    IToolCommandBuilder WithWorkingDirectory(string dir);
    IToolCommandBuilder WithStatus(string status, AppState state = AppState.Loading);
    IToolCommandBuilder WithTimer(bool show);
    IToolCommandBuilder WithOutputHandler(Func<string, bool>? handler);
    IToolCommandBuilder WithErrorHandler(Func<string, bool>? handler);

    IToolCommandBuilder Add(string literal);
    IToolCommandBuilder Add(params string[] literals);
    IToolCommandBuilder AddRange(IEnumerable<string> literals);
    IToolCommandBuilder AddIf(bool condition, string literal);
    IToolCommandBuilder AddIfNotNull(string? literal);
    
    IToolCommandBuilder AddPath(string path);
    IToolCommandBuilder AddPaths(IEnumerable<string> paths);
    
    IToolCommandBuilder AddOption(string flag, string value);
    IToolCommandBuilder AddOptionIfNotNull(string flag, string? value);
    
    IToolCommandBuilder AddPathOption(string flag, string path);
    IToolCommandBuilder AddPathOptionIfNotNull(string flag, string? path);
    
    IToolCommandBuilder AddScript(string template, params (string placeholder, string value)[] literals);
    IToolCommandBuilder AddScript(string template, params (string placeholder, string value, bool isPath)[] mappings);
    
    IToolCommandBuilder AddRawArguments(string? rawArgs);
    IToolCommandBuilder AddPathFromMap<TKey>(TKey key, IDictionary<TKey, string> map) where TKey : notnull;

    ToolCommand Build();
}