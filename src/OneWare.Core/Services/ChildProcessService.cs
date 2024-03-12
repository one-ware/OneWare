using System.Diagnostics;
using Asmichi.ProcessManagement;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class ChildProcessService : IChildProcessService
{
    private readonly ILogger _logger;
    private readonly IApplicationStateService _applicationStateService;
    
    public ChildProcessService(ILogger logger, IApplicationStateService applicationStateService)
    {
        _logger = logger;
        _applicationStateService = applicationStateService;
    }
    
    private static ChildProcessStartInfo GetProcessStartInfo(string path, string workingDirectory, IReadOnlyCollection<string> arguments)
    {
        return new ChildProcessStartInfo()
        {
            FileName = path,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            StdOutputRedirection = OutputRedirection.OutputPipe,
            StdInputRedirection = InputRedirection.NullDevice,
            StdErrorRedirection = OutputRedirection.ErrorPipe,
        };
    }
    
    public async Task<(bool success, string output)> ExecuteShellAsync(string path, IReadOnlyCollection<string> arguments, string workingDirectory, string status, AppState state = AppState.Loading, bool showTimer = false, Action<string>? outputAction = null, Func<string, bool>? errorAction = null)
    {
        var success = true;

        var argumentString = string.Join(' ', arguments.Select(x =>
        {
            if (x.Contains(' ')) return $"\"{x}\"";
            return x;
        }));
        _logger.Log($"[{Path.GetFileName(workingDirectory)}]: {Path.GetFileNameWithoutExtension(path)} {argumentString}", ConsoleColor.DarkCyan, true, Brushes.CornflowerBlue);

        var output = string.Empty;
        
        var startInfo = GetProcessStartInfo(path, workingDirectory, arguments);

        ApplicationProcess? key = null;
        try
        {
            using var childProcess = ChildProcess.Start(startInfo);
        
            key = _applicationStateService.AddState(status, state, () => childProcess?.Kill());
            
            var start = DateTime.Now;
        
            var dispatcherTimer = showTimer ? new DispatcherTimer(new TimeSpan(0, 0, 0, 1), DispatcherPriority.Default,
                (sender, args) =>
                {
                    var time = DateTime.Now - start;
                    key.StatusMessage = $"{status} {(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
                }) : null;
        
            dispatcherTimer?.Start();

            _ = ReadStreamAsync(childProcess.StandardOutput, line =>
            {
                if (outputAction != null)
                {
                    outputAction(line);
                    return;
                }
                
                Dispatcher.UIThread.Post(() => _logger.Log(line, ConsoleColor.Black, true));
                output += line + '\n';
            });
            
            _ = ReadStreamAsync(childProcess.StandardError, line =>
            {
                if (errorAction != null)
                {
                    errorAction(line);
                    return;
                }
                
                Dispatcher.UIThread.Post(() => _logger.Error(line));
                output += line + '\n';
            });
            
            await childProcess.WaitForExitAsync();
            
            dispatcherTimer?.Stop();
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            success = false;
        }

        if (key != null)
        {
            if (key.Terminated) success = false;
            _applicationStateService.RemoveState(key);
        }

        return (success,output);
    }

    private static async Task ReadStreamAsync(Stream stream, Action<string> outputAction)
    {
        try
        {
            using var reader = new StreamReader(stream);
            await Task.Run(() =>
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!string.IsNullOrEmpty(line)) outputAction(line);
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}