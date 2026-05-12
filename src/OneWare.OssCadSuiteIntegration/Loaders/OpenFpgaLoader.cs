using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Loaders;

public class OpenFpgaLoader(ISettingsService settingsService, ILogger logger, IOutputService outputService, 
    IToolExecutionDispatcherService toolExecutionDispatcherService)
    : IFpgaLoader
{
    public const string LoaderId = "openFpgaLoader";
    public string Id => LoaderId;
    public string Name => "OpenFpgaLoader";

    public async Task DownloadAsync(UniversalFpgaProjectRoot project)
    {
        var fpga = project.Properties.GetString("Fpga") ?? "unknown";
        var longTerm = settingsService.GetSettingValue<bool>("UniversalFpgaProjectSystem_LongTermProgramming");
        var properties = FpgaSettingsParser.LoadSettings(project, fpga);

        var board = properties.GetValueOrDefault("openFpgaLoaderBoard");
        var cable = properties.GetValueOrDefault("OpenFpgaLoader_Cable");
        var loaderPath = settingsService.GetSettingValue<string>(OssCadSuiteHelper.OpenFpgaLoaderPathSetting);
        var bitstreamFormat = properties
            .GetValueOrDefault("openFpgaLoaderBitstreamFormat", "bin");

        var command = toolExecutionDispatcherService.CreateToolCommandBuilder("openFPGALoader")
            .WithExecutable(loaderPath)
            .WithWorkingDirectory(project.FullPath)
            .WithStatus($"Running {loaderPath}...")
            .WithTimer(true)
            .WithOutputHandler(s =>
            {
                Dispatcher.UIThread.Post(() => { outputService.WriteLine(s); }); return true;
            })
            
            .AddOptionIfNotNull("-b", board)
            .AddOptionIfNotNull("-c", string.IsNullOrEmpty(board) ? cable : null) 
            
            .AddIf(longTerm, "-f")
            .AddRawArguments(longTerm 
                ? properties.GetValueOrDefault("openFpgaLoaderLongTermFlags") 
                : properties.GetValueOrDefault("openFpgaLoaderShortTermFlags"))
            .AddRawArguments(properties.GetValueOrDefault("OpenFpgaLoaderFlags"))
            
            .AddPath(bitstreamFormat switch {
                "bin" => "./build/pack.bin",
                "bit" => "./build/pack.bit",
                "fs" => "./build/pack.fs",
                _ => throw new ArgumentException("Invalid format")
            }).Build();
        outputService.WriteLine("Starting OpenFpgaLoader ...");
        
        try 
        {
            await toolExecutionDispatcherService.ExecuteAsync(command);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => 
            {
                outputService.WriteLine($"Error in ExecuteAsync: {ex.Message}");
                if (ex.InnerException != null)
                {
                    outputService.WriteLine($"Details: {ex.InnerException.Message}");
                }
            });
        }
    }
}