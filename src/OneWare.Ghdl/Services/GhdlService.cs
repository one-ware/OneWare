using System.Diagnostics;
using Avalonia.Media;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Output;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;

namespace OneWare.Ghdl.Services;

public class GhdlService
{
    private readonly ILogger _logger;
    private readonly IActive _active;
    
    public GhdlService(ILogger logger, IActive active)
    {
        _logger = logger;
        _active = active;
    }
    
    private static ProcessStartInfo GetGhdlProcessStartInfo(string workingDirectory, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = @"C:\Users\Hendrik\VHDPlus\Packages\ghdl\GHDL\bin\ghdl.exe",
            Arguments = $"{arguments}",
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
    }
    
    private async Task<bool> ExecuteGhdlShellAsync(string workingDirectory, string arguments, string status = "Running GHDL", AppState state = AppState.Loading)
    {
        var success = true;
        
        _logger.Log($"ghdl {arguments}", ConsoleColor.DarkCyan, true, Brushes.CornflowerBlue);

        var startInfo = GetGhdlProcessStartInfo(workingDirectory, arguments);

        using var activeProcess = new Process { StartInfo = startInfo };
        var key = _active.AddState(status, state, activeProcess);

        activeProcess.OutputDataReceived += (o, i) =>
        {
            if (string.IsNullOrEmpty(i.Data)) return;
            if (i.Data.Contains("error"))
            {
                success = false;
                _logger.Error(i.Data);
            }
            else if (i.Data.Contains("warning"))
            {
                _logger.Warning(i.Data);
            }
            else
            {
                _logger.Log(i.Data);
            }
        };
        activeProcess.ErrorDataReceived += (o, i) =>
        {
            if (!string.IsNullOrWhiteSpace(i.Data))
            {
                if (i.Data.Contains("warning", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Warning("[GHDL Warning]: " + i.Data);
                    //ParseGhdlError(i.Data, ErrorType.Warning);
                }
                else
                {
                    success = false;
                    _logger.Error("[GHDL Error]: " + i.Data);
                    //ParseGhdlError(i.Data, ErrorType.Error);
                }
            }
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

        return success;
    }

    public async Task SimulateFileAsync(IProjectFile file)
    {
        var vhdlFiles = string.Join(' ',
            file.Root.Files.Where(x => x.Extension is ".vhd" or ".vhdl")
                .Select(x => "\"" + x.FullPath + "\""));

        var top = Path.GetFileNameWithoutExtension(file.FullPath);
        var waveFormFileArgument = $"--vcd={top}.vcd";
        var ghdlOptions = "--std=02";
        var simulatingOptions = "--ieee-asserts=disable";
        var folder = file.TopFolder!.FullPath;
        
        var initFiles = await ExecuteGhdlShellAsync(folder, $"-i {ghdlOptions} {vhdlFiles}",
            "GHDL Initializing generated files");
        var make = initFiles &&
                   await ExecuteGhdlShellAsync(folder, $"-m {ghdlOptions} {top}", "Running GHDL Make");
        var elaboration = make && await ExecuteGhdlShellAsync(folder, $"-e {ghdlOptions} {top}",
            "Running GHDL Elaboration");
                    
        var run = elaboration && await ExecuteGhdlShellAsync(folder,
            $"-r {ghdlOptions} {top} {waveFormFileArgument} {simulatingOptions}",
            "Running GHDL Simulation");
    }
}