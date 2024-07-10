using Avalonia.Controls;
using Avalonia.Media;

namespace OneWare.Essentials.Services;

public interface ILogger
{
    public void WriteLogFile(string value);
    public void Log(object message, ConsoleColor color = default, bool showOutput = false, IBrush? outputBrush = null);
    public void Warning(string message, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null);
    public void Error(string message, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null);
}