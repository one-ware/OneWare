using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Core.Data; // Assuming Global and other data classes are here
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services; // Ensure these interfaces are correctly mapped to your project

namespace OneWare.Core.Services;

public class Logger : ILogger
{
    private static readonly DateTime AppStart = DateTime.Now;

    private static TextWriter? _log; // Consider making this non-static if you want multiple loggers for different purposes.
                                     // For a single application logger, static might be acceptable but manage its lifecycle carefully.

    private readonly IPaths _paths;
    private readonly IOutputService _outputService; // Injected dependency
    private readonly IDockService _dockService;     // Injected dependency
    private readonly IWindowService _windowService; // Injected dependency

    public Logger(IPaths paths, IOutputService outputService, IDockService dockService, IWindowService windowService)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _outputService = outputService ?? throw new ArgumentNullException(nameof(outputService));
        _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));

        Init();
    }

    private string LogFilePath => Path.Combine(_paths.DocumentsDirectory, "IDELog.txt");

    public void WriteLogFile(string value)
    {
        var date = DateTimeOffset.Now;
        if (_log != null)
            lock (_log) // Lock for thread safety on static TextWriter
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
        Warning(message, null, exception, showOutput, showDialog, dialogOwner);
    }

    public void Error(string message, Exception? exception = null, bool showOutput = true, bool showDialog = false,
        Window? dialogOwner = null)
    {
        Error(message, null, exception, showOutput, showDialog, dialogOwner);
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
        // Use the injected _outputService directly
        if (writeOutput) // No need to check IsRegistered, because it's injected (assuming it's always needed)
        {
            _outputService.WriteLine(message.ToString() ?? "", outputBrush);
        }
        WriteLogFile(message?.ToString() ?? "");
    }

    public void Error(string message, IProjectRoot? project, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        var output = message + (exception != null ? $"\n{exception}" : "");
        Log(output, project, ConsoleColor.Red);

        if (showOutput) // No need to check IsRegistered, it's injected
        {
            Dispatcher.UIThread.Post(() =>
            {
                _outputService.WriteLine(output, Brushes.Red); // Use injected service
                _dockService.Show(_outputService);           // Use injected service
            });
        }

        if (showDialog)
            Dispatcher.UIThread.Post(() =>
            {
                _ = _windowService.ShowMessageAsync("Error", output, MessageBoxIcon.Error, dialogOwner); // Use injected service
            });
    }

    public void Warning(string message, IProjectRoot? project, Exception? exception = null, bool showOutput = true,
        bool showDialog = false, Window? dialogOwner = null)
    {
        var output = message + (exception != null ? $"\n{exception}" : "");
        Log(output, project, ConsoleColor.Yellow);

        if (showOutput)
        {
            _outputService.WriteLine(output, Brushes.Orange); // Use injected service
            _dockService.Show(_outputService);               // Use injected service
        }

        if (showDialog)
            _ = _windowService.ShowMessageAsync("Warning", output, MessageBoxIcon.Warning, dialogOwner); // Use injected service
    }

    private void Init()
    {
        try
        {
            Directory.CreateDirectory(_paths.DocumentsDirectory);
            _log = File.CreateText(LogFilePath);
            PlatformHelper.ChmodFile(LogFilePath); // Ensure PlatformHelper exists and is accessible

            Log(
                $"Version: {Global.VersionCode} OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}", ConsoleColor.Cyan);
        }
        catch (Exception ex)
        {
            // Fallback for logging if file creation fails.
            // Avoid using the injected logger here as it's still being initialized.
            Console.WriteLine($"Can't create/access log file! Error: {ex.Message}");
        }
    }
}