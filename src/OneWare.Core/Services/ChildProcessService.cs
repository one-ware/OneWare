using System.Collections.Concurrent;
using System.Diagnostics;
using Asmichi.ProcessManagement;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class ChildProcessService(
    ILogger logger,
    IApplicationStateService applicationStateService,
    IOutputService outputService)
    : IChildProcessService
{
    private const int OutputSpeed = 10;

    private readonly ConcurrentDictionary<string, List<IChildProcess>> _childProcesses = [];
    private readonly TimeSpan _outputInterval = TimeSpan.FromMilliseconds(1);

    public IChildProcess StartChildProcess(ChildProcessStartInfo startInfo)
    {
        var process = ChildProcess.Start(startInfo);
        var key = startInfo.FileName ?? "";
        _childProcesses.TryAdd(key, []);
        _childProcesses[key].Add(process);
        _ = WaitForExitAsync(key, process);
        return process;
    }

    public IEnumerable<IChildProcess> GetChildProcesses(string path)
    {
        if (_childProcesses.TryGetValue(path, out var list)) return list;
        return Array.Empty<IChildProcess>();
    }

    public void Kill(params IChildProcess[] childProcesses)
    {
        foreach (var childProcess in childProcesses)
            try
            {
                childProcess.Kill();
            }
            catch (Exception e)
            {
                logger.Error(e.Message, e);
            }
    }

    public async Task<(bool success, string output)> ExecuteShellAsync(string path,
        IReadOnlyCollection<string> arguments, string workingDirectory, string status,
        AppState state = AppState.Loading, bool showTimer = false, Func<string, bool>? outputAction = null,
        Func<string, bool>? errorAction = null)
    {
        var success = true;

        //var fullPath = PlatformHelper.GetFullPath(path);
        //if(fullPath != null) 
        //    PlatformHelper.ChmodFile(fullPath);

        var argumentString = string.Join(' ', arguments.Select(x =>
        {
            if (x.Contains(' ')) return $"\"{x}\"";
            return x;
        }));
        logger.Log($"[{Path.GetFileName(workingDirectory)}]: {Path.GetFileNameWithoutExtension(path)} {argumentString}",
            true, Brushes.CornflowerBlue);

        var output = string.Empty;

        var startInfo = GetProcessStartInfo(path, workingDirectory, arguments);

        ApplicationProcess? key = null;
        try
        {
            ConcurrentQueue<string?> errorCollector = new();
            ConcurrentQueue<string?> outputCollector = new();

            var tokenSource = new CancellationTokenSource();

            var childProcess = StartChildProcess(startInfo);

            key = applicationStateService.AddState(status, state, () => tokenSource.Cancel());

            var start = DateTime.Now;

            var statusTimer = showTimer
                ? new DispatcherTimer(new TimeSpan(0, 0, 0, 1), DispatcherPriority.Default,
                    (_, _) =>
                    {
                        var time = DateTime.Now - start;
                        key.StatusMessage = $"{status} {(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
                    })
                : null;

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
                                // ReSharper disable once AccessToModifiedClosure
                                success = success && outputAction(outputLine);
                                output += outputLine + '\n';
                                return;
                            }

                            outputService.WriteLine(outputLine);
                            output += outputLine + '\n';
                        }

                        if (errorCollector.TryDequeue(out var errorLine))
                        {
                            if (errorLine == null) return;

                            if (errorAction != null)
                            {
                                success = success && errorAction(errorLine);
                                output += errorLine + '\n';
                                return;
                            }

                            success = false;
                            logger.Error(errorLine);
                            output += errorLine + '\n';
                        }
                    }
                });

            collectorTimer.Start();

            _ = CollectOutputFromStreamAsync(childProcess.StandardOutput, outputCollector);
            _ = CollectOutputFromStreamAsync(childProcess.StandardError, errorCollector);

            try
            {
                await childProcess.WaitForExitAsync(tokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                logger.Log(
                    $"[{Path.GetFileName(workingDirectory)}]: {Path.GetFileNameWithoutExtension(path)} cancelled!",
                    true, Brushes.DarkOrange);
                childProcess.Kill();
            }

            //Delay until collector empty
            while (!outputCollector.IsEmpty || !errorCollector.IsEmpty) await Task.Delay(50);

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

        return (success, output);
    }

    public WeakReference<Process> StartWeakProcess(string path, IReadOnlyCollection<string> arguments,
        string workingDirectory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(path, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.EnableRaisingEvents = true;

        return new WeakReference<Process>(process);
    }

    private static ChildProcessStartInfo GetProcessStartInfo(string path, string workingDirectory,
        IReadOnlyCollection<string> arguments)
    {
        return new ChildProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            StdOutputRedirection = OutputRedirection.OutputPipe,
            StdInputRedirection = InputRedirection.NullDevice,
            StdErrorRedirection = OutputRedirection.ErrorPipe
        };
    }

    private async Task WaitForExitAsync(string path, IChildProcess childProcess)
    {
        await Task.Run(childProcess.WaitForExit);
        if (_childProcesses.TryGetValue(path, out var list))
        {
            list.Remove(childProcess);
            if (list.Count == 0) _childProcesses.Remove(path, out _);
        }
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