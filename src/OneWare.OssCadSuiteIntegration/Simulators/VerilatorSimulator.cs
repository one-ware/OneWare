using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.OssCadSuiteIntegration.ViewModels;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Context;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Simulators;

public class VerilatorSimulator : IFpgaSimulator
{
    private static readonly HashSet<string> WaveformExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".vcd", ".fst", ".lxt", ".lxt2" };

    private readonly GtkWaveService _gtkWaveService;
    private readonly ILogger _logger;
    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IToolExecutionDispatcherService _toolExecutionDispatcherService;

    public VerilatorSimulator(ILogger logger, IMainDockService mainDockService,
        IProjectExplorerService projectExplorerService, GtkWaveService gtkWaveService,
        IToolExecutionDispatcherService toolExecutionDispatcherService)
    {
        _logger = logger;
        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;
        _gtkWaveService = gtkWaveService;
        _toolExecutionDispatcherService = toolExecutionDispatcherService;

        TestBenchToolbarTopUiExtension = new OneWareUiExtension(x =>
        {
            if (x is not TestBenchContext context) return null;
            return new VerilatorSimulatorToolbarView
            {
                DataContext = new VerilatorSimulatorToolbarViewModel(context)
            };
        });
    }

    public string Name => "Verilator";

    public OneWareUiExtension? TestBenchToolbarTopUiExtension { get; }

    public async Task<bool> SimulateAsync(string fullPath)
    {
        if (_projectExplorerService.GetRootFromFile(fullPath) is not UniversalFpgaProjectRoot root) return false;

        var settings = await TestBenchContextManager.LoadContextAsync(fullPath);
        var topModule = settings.GetBenchProperty(nameof(VerilatorSimulatorToolbarViewModel.TopModule));
        if (string.IsNullOrWhiteSpace(topModule))
            topModule = Path.GetFileNameWithoutExtension(fullPath);

        var sourceFiles = HdlSimulationSourceHelper.GetOrderedSources(root, fullPath);

        if (sourceFiles.Count == 0)
        {
            _logger.Warning("Verilator simulation requires at least one Verilog or SystemVerilog source file.");
            return false;
        }

        var buildDirectory = Path.Combine(root.FullPath, "build", "sim", "verilator",
            Path.GetFileNameWithoutExtension(fullPath));
        Directory.CreateDirectory(buildDirectory);
        var executableName = $"simulation{PlatformHelper.ExecutableExtension}";
        var executablePath = Path.Combine(buildDirectory, executableName);

        _mainDockService.Show<IOutputService>();

        var compileCommand = _toolExecutionDispatcherService.CreateToolCommandBuilder("verilator")
            .WithWorkingDirectory(root.FullPath)
            .WithStatus("Compiling with Verilator...", AppState.Loading)
            .WithTimer(true)
            .Add("--binary", "--timing", "--trace")
            .AddRawArguments(settings.GetBenchProperty(
                nameof(VerilatorSimulatorToolbarViewModel.VerilatorArguments)))
            .AddOption("--top-module", topModule)
            .AddPathOption("--Mdir", buildDirectory)
            .AddOption("-o", executableName)
            .AddPaths(sourceFiles)
            .Build();

        var (compiled, _) = await _toolExecutionDispatcherService.ExecuteAsync(compileCommand);
        if (!compiled)
        {
            _logger.LogWarning("Verilator compilation failed");
            return false;
        }

        if (!File.Exists(executablePath))
        {
            _logger.Error($"Verilator did not create the expected executable: {executablePath}");
            return false;
        }

        var simulationStarted = DateTime.UtcNow;
        var runCommand = _toolExecutionDispatcherService.CreateToolCommandBuilder("verilator")
            .WithExecutable(executablePath)
            .WithWorkingDirectory(root.FullPath)
            .WithStatus("Running Verilator simulation...", AppState.Loading)
            .WithTimer(true)
            .AddRawArguments(settings.GetBenchProperty(
                nameof(VerilatorSimulatorToolbarViewModel.VerilatorRuntimeArguments)))
            .Build();

        var (simulated, _) = await _toolExecutionDispatcherService.ExecuteAsync(runCommand);
        if (!simulated)
        {
            _logger.LogWarning("Verilator simulation failed");
            return false;
        }

        await OpenLatestWaveformAsync(root.FullPath, simulationStarted);
        return true;
    }

    private async Task OpenLatestWaveformAsync(string projectPath, DateTime simulationStarted)
    {
        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };
        var waveform = Directory.EnumerateFiles(projectPath, "*", options)
            .Where(x => WaveformExtensions.Contains(Path.GetExtension(x)))
            .Where(x => File.GetLastWriteTimeUtc(x) >= simulationStarted.AddSeconds(-1))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (waveform == null) return;

        if (Path.GetExtension(waveform).Equals(".vcd", StringComparison.OrdinalIgnoreCase))
            _ = await _mainDockService.OpenFileAsync(waveform);
        else
            _ = _gtkWaveService.OpenInGtkWaveAsync(waveform);
    }
}
