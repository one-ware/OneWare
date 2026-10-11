using System.Diagnostics;
using System.IO.Pipes;

internal sealed class StudioProcessController
{
    private const string PipeName = "oneware-studio-ipc";
    private const string ShutdownMessage = "shutdown";
    private static readonly string LockFilePath = Path.Combine(Path.GetTempPath(), "OneWare", "oneware-studio.lock");
    private static readonly string ExecutableExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
    private static readonly string StudioExecutableName = $"OneWareStudio{ExecutableExtension}";
    private static readonly string CliExecutableName = $"oneware{ExecutableExtension}";

    public Task<int> StartStudio(string? openTarget, bool detach, CancellationToken cancellationToken)
    {
        var studioPath = Path.Combine(AppContext.BaseDirectory, StudioExecutableName);
        var startInfo = new ProcessStartInfo(studioPath)
        {
            UseShellExecute = detach,
            WorkingDirectory = Environment.CurrentDirectory
        };

        if (!string.IsNullOrWhiteSpace(openTarget))
            startInfo.ArgumentList.Add(openTarget);

        return Task.FromResult(Process.Start(startInfo) is null ? 1 : 0);
    }

    public async Task<int> StopStudio(CancellationToken cancellationToken)
    {
        using var studioProcess = TryGetRunningStudioProcess();
        if (studioProcess is null)
        {
            Console.Error.WriteLine("ONE WARE Studio is not running.");
            return 0;
        }

        if (await TrySendToExistingInstanceAsync(ShutdownMessage, cancellationToken))
        {
            if (await WaitForExitAsync(studioProcess, TimeSpan.FromSeconds(10), cancellationToken))
                return 0;

            Console.Error.WriteLine("Timed out waiting for ONE WARE Studio to exit.");
        }
        else
        {
            Console.Error.WriteLine("Could not reach ONE WARE Studio over IPC.");
        }

        return 1;
    }

    private static Process? TryGetRunningStudioProcess()
    {
        if (TryReadRunningStudioProcessFromLockFile() is { } lockedProcess)
            return lockedProcess;

        var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, StudioExecutableName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, CliExecutableName))
        };

        foreach (var process in Process.GetProcesses())
            try
            {
                if (process.Id == Environment.ProcessId)
                {
                    process.Dispose();
                    continue;
                }

                var processPath = process.MainModule?.FileName;
                if (processPath is not null && expectedPaths.Contains(Path.GetFullPath(processPath)))
                    return process;

                process.Dispose();
            }
            catch
            {
                process.Dispose();
            }

        return null;
    }

    private static Process? TryReadRunningStudioProcessFromLockFile()
    {
        try
        {
            if (!File.Exists(LockFilePath))
                return null;

            var pidText = File.ReadAllText(LockFilePath).Trim();
            if (!int.TryParse(pidText, out var pid))
                return null;

            var process = Process.GetProcessById(pid);
            if (!process.HasExited)
                return process;

            process.Dispose();
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TrySendToExistingInstanceAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            using var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCancellation.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(connectCancellation.Token);

            await using var writer = new StreamWriter(client);
            await writer.WriteAsync(message.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);

            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (process.HasExited)
            return true;

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process.HasExited;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }
}
