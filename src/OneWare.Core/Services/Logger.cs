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
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), _paths.AppFolderName,
            "IDELog.txt");

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

    public void Log(object message, ConsoleColor color = default, bool showOutput = false, IBrush? outputBrush = null)
    {
        Log(message, null, color, showOutput, outputBrush);
    }

    public void Warning(string message, Exception? exception = null, bool showOutput = true, bool showDialog = false,
        Window? dialogOwner = null)
    {
        throw new NotImplementedException();
    }

    public void Error(string message, Exception? exception = null, bool showOutput = true, bool showDialog = false,
        Window? dialogOwner = null)
    {
        throw new NotImplementedException();
    }

    public void Log(object message, IProjectRoot? project, ConsoleColor color = default, bool writeOutput = false, IBrush? outputBrush = null)
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
        if (writeOutput && ContainerLocator.Container.IsRegistered<IOutputService>())
            ContainerLocator.Current.Resolve<IOutputService>().WriteLine(message.ToString() ?? "", outputBrush);
        WriteLogFile(message?.ToString() ?? "");
    }

    public void Error(string message, IProjectRoot? project, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        var output = message + (exception != null ? $"\n{exception}" : "");
        Log(output, project, ConsoleColor.Red);

        if (showOutput && ContainerLocator.Container.IsRegistered<IOutputService>())
            Dispatcher.UIThread.Post(() =>
            {
                ContainerLocator.Current.Resolve<IOutputService>().WriteLine(output, Brushes.Red);
                ContainerLocator.Current.Resolve<IDockService>()
                    .Show(ContainerLocator.Current.Resolve<IOutputService>());
            });

        if (showDialog)
            Dispatcher.UIThread.Post(() =>
            {
                _ = ContainerLocator.Current.Resolve<IWindowService>()
                    .ShowMessageAsync("Error", output, MessageBoxIcon.Error, dialogOwner);
            });
    }

    public void Warning(string message, IProjectRoot? project, Exception? exception = null,   bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        var output = message + (exception != null ? $"\n{exception}" : "");
        Log(output, project, ConsoleColor.Yellow);

        if (showOutput && ContainerLocator.Container.IsRegistered<IOutputService>())
        {
            ContainerLocator.Current.Resolve<IOutputService>().WriteLine(output, Brushes.Orange);
            ContainerLocator.Current.Resolve<IDockService>().Show(ContainerLocator.Current.Resolve<IOutputService>());
        }

        if (showDialog)
            _ = ContainerLocator.Current.Resolve<IWindowService>()
                .ShowMessageAsync("Warning", output, MessageBoxIcon.Warning, dialogOwner);
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