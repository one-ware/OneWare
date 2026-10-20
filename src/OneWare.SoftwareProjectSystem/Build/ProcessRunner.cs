using System.Diagnostics;

namespace OneWare.SoftwareProjectSystem.Build;

/// <summary>
/// Runs a tool and streams its output line by line. Cancelling the token kills the process tree.
/// </summary>
public static class ProcessRunner
{
    public static async Task<int> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, Action<string> onOutput, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process();
        process.StartInfo = startInfo;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) onOutput(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) onOutput(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            throw;
        }

        // WaitForExitAsync returns before the redirected streams are drained; the parameterless
        // WaitForExit waits for them.
        process.WaitForExit();
        return process.ExitCode;
    }
}
