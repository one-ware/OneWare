using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using OneWare.Output;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;

namespace OneWare.Core.Services;

public class Logger : ILogger
{
    private static readonly DateTime AppStart = DateTime.Now;

    private static TextWriter? _log;

    private string LogFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), _paths.AppFolderName,
            "IDELog.txt");

    private readonly IPaths _paths;
    public Logger(IPaths paths)
    {
        _paths = paths;
    }
    
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
    
    public void Log(object message, ConsoleColor color = ConsoleColor.White, bool writeOutput = false, IBrush? outputBrush = null)
    {
        var appRun = DateTime.Now - AppStart;
#if DEBUG
        if(RuntimeInformation.ProcessArchitecture is not Architecture.Wasm) Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write(@"[" + string.Format("{0:D2}:{1:D2}:{2:D2}", (int)appRun.TotalHours,
            (int)appRun.TotalMinutes, appRun.Seconds) + @"] ");
        if(RuntimeInformation.ProcessArchitecture is not Architecture.Wasm) Console.ForegroundColor = color;
        Console.WriteLine(message);
        if(RuntimeInformation.ProcessArchitecture is not Architecture.Wasm) Console.ForegroundColor = ConsoleColor.White;
#endif
        if (writeOutput && ContainerLocator.Container.IsRegistered<IOutputService>())
        {
            ContainerLocator.Current.Resolve<IOutputService>().WriteLine(message.ToString() ?? "", outputBrush);
        }
        WriteLogFile(message?.ToString() ?? "");
    }

    public static string CurrentTimeString()
    {
        var time = DateTime.Now;
        return "[" + $"{time.Hour:D2}:{time.Minute:D2}:{time.Second:D2}" + "]";
    }

    public void Error(string message, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        Log(message + "\n" + exception, ConsoleColor.Red);

        if (showOutput && ContainerLocator.Container.IsRegistered<IOutputService>())
        {
            ContainerLocator.Current.Resolve<IOutputService>().WriteLine("[Error]: " + message, Brushes.Red);
            ContainerLocator.Current.Resolve<IDockService>().Show(ContainerLocator.Current.Resolve<IOutputService>());
        }

        if (showDialog)
        {
            _ = ContainerLocator.Current.Resolve<IWindowService>()
                .ShowMessageAsync("Error", message, MessageBoxIcon.Error, dialogOwner);
        }
    }

    public void Warning(string message, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        Log(message + "\n" + exception, ConsoleColor.Yellow);

        if (showOutput && ContainerLocator.Container.IsRegistered<IOutputService>())
        {
            ContainerLocator.Current.Resolve<IOutputService>().WriteLine("[Warning]: " + message, Brushes.Orange);
            ContainerLocator.Current.Resolve<IDockService>().Show(ContainerLocator.Current.Resolve<IOutputService>());
        }
        
        if (showDialog)
        {
            _ = ContainerLocator.Current.Resolve<IWindowService>()
                .ShowMessageAsync("Warning", message, MessageBoxIcon.Warning, dialogOwner);
        }
    }

    public void Init()
    {
        try
        {
            Directory.CreateDirectory(_paths.DocumentsDirectory);
            _log = File.CreateText(LogFilePath);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Tools.ExecBash("chmod 777 " + LogFilePath);
        }
        catch
        {
            Console.WriteLine("Can't create/access log file!");
        }
    }
}