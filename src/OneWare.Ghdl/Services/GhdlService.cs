using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Output;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;
using OneWare.Vcd.Viewer.ViewModels;
using ReactiveUI;

namespace OneWare.Ghdl.Services;

public class GhdlService
{
    private readonly ILogger _logger;
    private readonly IActive _active;
    private readonly IDockService _dockService;
    private readonly IProjectExplorerService _projectExplorerService;

    public AsyncRelayCommand SimulateCommand { get; }
    
    public GhdlService(ILogger logger, IActive active, IDockService dockService, IProjectExplorerService projectExplorerService)
    {
        _logger = logger;
        _active = active;
        _dockService = dockService;
        _projectExplorerService = projectExplorerService;
        
        SimulateCommand = new AsyncRelayCommand(SimulateCurrentFileAsync, 
            () => _dockService.CurrentDocument?.CurrentFile?.Extension is ".vhd" or ".vhdl");

        _dockService.WhenValueChanged(x => x.CurrentDocument).Subscribe(x =>
        {
            SimulateCommand.NotifyCanExecuteChanged();
        });
    }
    
    private static ProcessStartInfo GetGhdlProcessStartInfo(string workingDirectory, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = "/home/hendrik/VHDPlus/Packages/ghdl/bin/ghdl", //@"C:\Users\Hendrik\VHDPlus\Packages\ghdl\GHDL\bin\ghdl.exe",
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

    private Task SimulateCurrentFileAsync()
    {
        if (_dockService.CurrentDocument?.CurrentFile is IProjectFile selectedFile)
            return SimulateFileAsync(selectedFile);
        return Task.CompletedTask;
    }
    
    public async Task SimulateFileAsync(IProjectFile file)
    {
        _dockService.Show<IOutputService>();
        
        var vhdlFiles = string.Join(' ',
            file.Root.Files.Where(x => x.Extension is ".vhd" or ".vhdl")
                .Select(x => "\"" + x.FullPath + "\""));

        var top = Path.GetFileNameWithoutExtension(file.FullPath);
        var vcdPath = $"{top}.vcd";
        var waveFormFileArgument = $"--vcd={vcdPath}";
        var ghdlOptions = "--std=02";
        var simulatingOptions = "--ieee-asserts=disable";
        var folder = file.TopFolder!.FullPath;
        
        var initFiles = await ExecuteGhdlShellAsync(folder, $"-i {ghdlOptions} {vhdlFiles}",
            "GHDL Initializing generated files");
        var make = initFiles &&
                   await ExecuteGhdlShellAsync(folder, $"-m {ghdlOptions} {top}", "Running GHDL Make");
        var elaboration = make && await ExecuteGhdlShellAsync(folder, $"-e {ghdlOptions} {top}",
            "Running GHDL Elaboration");
                    
        var openFile = file.TopFolder.Search($"{top}.vcd") as IProjectFile;
        openFile ??= file.TopFolder.AddFile(vcdPath, true);
        
        var doc = await _dockService.OpenFileAsync(openFile);
        if (doc is VcdViewModel vcd)
        {
            vcd.PrepareLiveStream();
        }
        
        var run = elaboration && await ExecuteGhdlShellAsync(folder,
            $"-r {ghdlOptions} {top} {waveFormFileArgument} {simulatingOptions}",
            "Running GHDL Simulation");
    }
}