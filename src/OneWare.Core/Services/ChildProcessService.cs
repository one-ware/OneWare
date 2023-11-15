using System.Diagnostics;
using Avalonia.Media;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;

namespace OneWare.Core.Services;

public class ChildProcessService : IChildProcessService
{
    private readonly ILogger _logger;
    private readonly IActive _active;
    
    public ChildProcessService(ILogger logger, IActive active)
    {
        _logger = logger;
        _active = active;
    }
    
    private static ProcessStartInfo GetProcessStartInfo(string workingDirectory, string path, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = path,
            Arguments = $"{arguments}",
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
    }
    
    public async Task<(bool success, string output)> ExecuteShellAsync(string path, string arguments, string workingDirectory, string status, AppState state = AppState.Loading)
    {
        var success = true;
        
        _logger.Log($"{Path.GetFileNameWithoutExtension(path)} {arguments}", ConsoleColor.DarkCyan, true, Brushes.CornflowerBlue);

        var output = string.Empty;
        
        var startInfo = GetProcessStartInfo(path, workingDirectory, arguments);

        using var activeProcess = new Process();
        activeProcess.StartInfo = startInfo;
        var key = _active.AddState(status, state, activeProcess);

        activeProcess.OutputDataReceived += (o, i) =>
        {
            if (string.IsNullOrEmpty(i.Data)) return;
            _logger.Log(i.Data, ConsoleColor.Black, true);
            output += i.Data + '\n';
        };
        activeProcess.ErrorDataReceived += (o, i) =>
        {
            if (string.IsNullOrEmpty(i.Data)) return;
            success = false;
            _logger.Warning(i.Data, null, false);
            output += i.Data + '\n';
        };

        try
        {
            activeProcess.Start();
            activeProcess.BeginOutputReadLine();
            activeProcess.BeginErrorReadLine();

            await Task.Run(() => activeProcess.WaitForExit());
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            success = false;
        }

        if (key.Terminated) success = false;
        _active.RemoveState(key);

        return (success,output);
    }
}