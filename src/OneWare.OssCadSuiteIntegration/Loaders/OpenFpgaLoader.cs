using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.Services;
using Microsoft.Extensions.Logging;

namespace OneWare.OssCadSuiteIntegration.Loaders;

public class OpenFpgaLoader(IChildProcessService childProcess, ISettingsService settingsService, ILogger logger)
    : IFpgaLoader
{
    public string Name => "OpenFpgaLoader";

    public async Task DownloadAsync(UniversalFpgaProjectRoot project)
    {
        var fpga = project.GetProjectProperty("Fpga") ?? "unknown";

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
        openFpgaLoaderArguments.Add("./build/pack.bin");

        await childProcess.ExecuteShellAsync("openFPGALoader", openFpgaLoaderArguments,
            project.FullPath, "Running OpenFPGALoader...", AppState.Loading, true);
    }
}