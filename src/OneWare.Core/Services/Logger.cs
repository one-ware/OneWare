using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Core.Data;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Core.Services;

public class Logger : ILogger
{
    private static readonly DateTime AppStart = DateTime.Now;

    private static TextWriter? _log;

    private readonly IPaths _paths;

    public Logger(IPaths paths)
    {
        _paths = paths;
        Init();
    }

    private string LogFilePath =>
        Path.Combine(_paths.DocumentsDirectory, "IDELog.txt");

    public void WriteLogFile(string value)
    {
        var date = DateTimeOffset.Now;
        if (_log != null)
            lock (_log)
            {
                _log.WriteLine($"{date:dd-MMM-yyyy HH:mm:ss.fff}> {value}");
                _log.Flush();
            }
    }

    public void Log(object message, ConsoleColor color = default)
    {
        Log(message, null, color);
    }

    public void Warning(string message, Exception? exception = null)
    {
        Warning(message, null, exception);
    }

    public void Error(string message, Exception? exception = null)
    {
        Error(message, null, exception);
    }

    public void Log(object message, IProjectRoot? project, ConsoleColor color = default)
    {
        var appRun = DateTime.Now - AppStart;
#if DEBUG
        if (RuntimeInformation.ProcessArchitecture is not Architecture.Wasm)
            Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write(@"[" + string.Format("{0:D2}:{1:D2}:{2:D2}", (int)appRun.TotalHours,
            (int)appRun.TotalMinutes, appRun.Seconds) + @"] ");
        if (RuntimeInformation.ProcessArchitecture is not Architecture.Wasm) Console.ForegroundColor = color;
        Console.WriteLine(message);
        if (RuntimeInformation.ProcessArchitecture is not Architecture.Wasm) Console.ForegroundColor = default;
#endif
        WriteLogFile(message?.ToString() ?? "");
    }

    public void Error(string message, IProjectRoot? project, Exception? exception = null)
    {
        var output = message + (exception != null ? $"\n{exception}" : "");
        Log(output, project, ConsoleColor.Red);
    }

    public void Warning(string message, IProjectRoot? project, Exception? exception = null)
    {
        var output = message + (exception != null ? $"\n{exception}" : "");
        Log(output, project, ConsoleColor.Yellow);
    }

    private void Init()
    {
        try
        {
            Directory.CreateDirectory(_paths.DocumentsDirectory);
            _log = File.CreateText(LogFilePath);
            PlatformHelper.ChmodFile(LogFilePath);

            Log(
                $"Version: {Global.VersionCode} OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}", ConsoleColor.Cyan);
        }
        catch
        {
            Console.WriteLine("Can't create/access log file!");
        }
    }
}