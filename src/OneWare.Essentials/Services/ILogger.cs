using Avalonia.Controls;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface ILogger
{
    public void WriteLogFile(string value);
    
    public void Log(object message, ConsoleColor color = default);

    public void Log(object message, IProjectRoot? owner, ConsoleColor color = default);

    public void Warning(string message, Exception? exception = null);
    
    public void Warning(string message, IProjectRoot? owner, Exception? exception = null);

    public void Error(string message, Exception? exception = null);
    
    public void Error(string message, IProjectRoot? owner, Exception? exception = null);
}