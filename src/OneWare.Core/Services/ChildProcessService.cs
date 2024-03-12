using System.Collections.Concurrent;
using Asmichi.ProcessManagement;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class ChildProcessService(ILogger logger, IApplicationStateService applicationStateService)
    : IChildProcessService
{
    private const int OutputSpeed = 10;
    private readonly TimeSpan _outputInterval = TimeSpan.FromMilliseconds(1);

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
    
    public async Task<(bool success, string output)> ExecuteShellAsync(string path, IReadOnlyCollection<string> arguments, string workingDirectory, string status, AppState state = AppState.Loading, bool showTimer = false, Func<string, bool>? outputAction = null, Func<string, bool>? errorAction = null)
    {
        var success = true;

        var argumentString = string.Join(' ', arguments.Select(x =>
        {
            if (x.Contains(' ')) return $"\"{x}\"";
            return x;
        }));
        logger.Log($"[{Path.GetFileName(workingDirectory)}]: {Path.GetFileNameWithoutExtension(path)} {argumentString}", ConsoleColor.DarkCyan, true, Brushes.CornflowerBlue);

        var output = string.Empty;
        
        var startInfo = GetProcessStartInfo(path, workingDirectory, arguments);

        ApplicationProcess? key = null;
        try
        {
            ConcurrentQueue<string?> errorCollector = new();
            ConcurrentQueue<string?> outputCollector = new();
            
            var tokenSource = new CancellationTokenSource();
            
            using var childProcess = ChildProcess.Start(startInfo);
        
            key = applicationStateService.AddState(status, state, () => tokenSource.Cancel());
            
            var start = DateTime.Now;
        
            var statusTimer = showTimer ? new DispatcherTimer(new TimeSpan(0, 0, 0, 1), DispatcherPriority.Default,
                (_, _) =>
                {
                    var time = DateTime.Now - start;
                    key.StatusMessage = $"{status} {(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
                }) : null;
        
            statusTimer?.Start();
            
            var collectorTimer = new DispatcherTimer(_outputInterval, DispatcherPriority.Default,
                (_, _) =>
                {
                    for (var i = 0; i < OutputSpeed; i++)
                    {
                        if (outputCollector.TryDequeue(out var outputLine))
                        {
                            if (outputLine == null) return;
                        
                            if (outputAction != null)
                            {
                                outputAction(outputLine);
                                return;
                            }

                            logger.Log(outputLine, ConsoleColor.Black, true);
                            output += outputLine + '\n';
                        }
                    
                        if (errorCollector.TryDequeue(out var errorLine))
                        {
                            if (errorLine == null) return;
                        
                            if (errorAction != null)
                            {
                                errorAction(errorLine);
                                return;
                            }

                            logger.Error(errorLine);
                            output += errorLine + '\n';
                        }
                    }
                });

            collectorTimer.Start();

            _ = CollectOutputFromStreamAsync(childProcess.StandardOutput, outputCollector);
            _ = CollectOutputFromStreamAsync(childProcess.StandardError, errorCollector);
            
            await childProcess.WaitForExitAsync(tokenSource.Token);
            
            if(tokenSource.IsCancellationRequested)
                childProcess.Kill();

            //Delay until collector empty
            while (!outputCollector.IsEmpty || !errorCollector.IsEmpty)
            {
                await Task.Delay(50);
            }
            
            collectorTimer.Stop();
            statusTimer?.Stop();
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            success = false;
        }

        if (key != null)
        {
            if (key.Terminated) success = false;
            applicationStateService.RemoveState(key);
        }

        return (success,output);
    }

    private static async Task CollectOutputFromStreamAsync(Stream stream, ConcurrentQueue<string?> collector)
    {
        try
        {
            using var reader = new StreamReader(stream);
            await Task.Run(() =>
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    collector.Enqueue(line);
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}