using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Loaders;

public class OpenFpgaLoader(IChildProcessService childProcess, 
    ISettingsService settingsService, ILogger logger, IOutputService outputService, 
    IToolExecutionDispatcherService toolExecutionDispatcherService)
    : IFpgaLoader
{
    public string Name => "OpenFpgaLoader";

    public async Task DownloadAsync(UniversalFpgaProjectRoot project)
    {
        var fpga = project.Properties.GetString("Fpga") ?? "unknown";

        var longTerm = settingsService.GetSettingValue<bool>("UniversalFpgaProjectSystem_LongTermProgramming");

        var properties = FpgaSettingsParser.LoadSettings(project, fpga);

        var board = properties.GetValueOrDefault("openFpgaLoaderBoard");
        var cable = properties.GetValueOrDefault("OpenFpgaLoader_Cable");

        List<string> openFpgaLoaderArguments = [];
        if (!string.IsNullOrEmpty(board))
        {
            openFpgaLoaderArguments.AddRange(["-b", board]);
        }
        else if (!string.IsNullOrEmpty(cable))
        {
            openFpgaLoaderArguments.AddRange(["-c", cable]);
        }
        else
        {
            logger.Error("Board/Cable not supported/configured for openFPGALoader!");
            return;
        }

        if (longTerm) openFpgaLoaderArguments.Add("-f");

        openFpgaLoaderArguments.AddRange(properties.GetValueOrDefault("OpenFpgaLoader_Flags")?.Split(' ',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);
        
        var bitstreamFormat = properties
            .GetValueOrDefault("openFpgaLoaderBitstreamFormat", "bin");
        
        switch (bitstreamFormat)
        {
            case "bin":
                openFpgaLoaderArguments.Add("./build/pack.bin");
                break;
            case "bit":
                openFpgaLoaderArguments.Add("./build/pack.bit");
                break;
            default:
                outputService.WriteLine($"Could not find output type: {bitstreamFormat}");
                return;
        }
        
        var path = settingsService.GetSettingValue<string>(OssCadSuiteIntegrationModule.OpenFpgaLoaderPathSetting);
        var command = ToolCommand.FromShellParams(path, openFpgaLoaderArguments,
            project.FullPath, $"Running {path}...", AppState.Loading, true, null, s =>
            {
                Dispatcher.UIThread.Post(() => { outputService.WriteLine(s); });
                return true;
            });
        
        await toolExecutionDispatcherService.ExecuteAsync(command);
        
        //await childProcess.ExecuteShellAsync(path, openFpgaLoaderArguments,
        //    project.FullPath, "Running OpenFPGALoader...", AppState.Loading, true);
    }
}