using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysService(
    IChildProcessService childProcessService,
    ILogger logger,
    IOutputService outputService,
    IDockService dockService)
{
    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        try
        {
            var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);
            
            var top = project.TopEntity?.Header ?? throw new Exception("TopEntity not set!");

            var includedFiles = project.Files
                .Where(x => x.Extension is ".v" or ".sv")
                .Where(x => !project.CompileExcluded.Contains(x))
                .Where(x => !project.TestBenches.Contains(x))
                .Select(x => x.RelativePath);

            var buildDir = Path.Combine(project.FullPath, "build");
            Directory.CreateDirectory(buildDir);

            dockService.Show<IOutputService>();

            var start = DateTime.Now;
            outputService.WriteLine("Compiling...\n==================");

            var yosysSynthTool = properties.GetValueOrDefault("YosysToolchain_Yosys_SynthTool") ?? throw new Exception("Yosys Tool not set!");
            
            List<string> yosysArguments =
                ["-q", "-p", $"{yosysSynthTool} -json build/synth.json"];
            yosysArguments.AddRange(properties.GetValueOrDefault("YosysToolchain_Yosys_Flags")?.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);
            yosysArguments.AddRange(includedFiles);

            var nextPnrTool = properties.GetValueOrDefault("YosysToolchain_NextPnr_Tool") ?? throw new Exception("NextPnr Tool not set!");
            List<string> nextPnrArguments =
                ["--json", "./build/synth.json", "--pcf", "project.pcf", "--asc", "./build/nextpnr.asc"];
            nextPnrArguments.AddRange(properties.GetValueOrDefault("YosysToolchain_NextPnr_Flags")?.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);

            var packTool = properties.GetValueOrDefault("YosysToolchain_Pack_Tool") ?? throw new Exception("Pack Tool not set!");;
            List<string> packToolArguments = ["./build/nextpnr.asc", "./build/pack.bin"];
            packToolArguments.AddRange(properties.GetValueOrDefault("YosysToolchain_Pack_Flags")?.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);

            var (success, _) = await childProcessService.ExecuteShellAsync("yosys", yosysArguments, project.FullPath,
                "Running yosys...", AppState.Loading, true, x =>
                {
                    if (x.StartsWith("Error:"))
                    {
                        logger.Error(x);
                        return false;
                    }
                    outputService.WriteLine(x);
                    return true;
                });

            success = success && (await childProcessService.ExecuteShellAsync(nextPnrTool, nextPnrArguments,
                project.FullPath, $"Running {nextPnrTool}...", AppState.Loading, true, null, s =>
                {
                    Dispatcher.UIThread.Post(() => { outputService.WriteLine(s); });
                    return true;
                })).success;

            success = success && (await childProcessService.ExecuteShellAsync(packTool, packToolArguments,
                project.FullPath,
                $"Running {packTool}...")).success;

            var compileTime = DateTime.Now - start;

            if (success)
                outputService.WriteLine(
                    $"==================\n\nCompilation finished after {(int)compileTime.TotalMinutes:D2}:{compileTime.Seconds:D2}\n");
            else
                outputService.WriteLine(
                    $"==================\n\nCompilation failed after {(int)compileTime.TotalMinutes:D2}:{compileTime.Seconds:D2}\n",
                    Brushes.Red);

            return success;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }

    public async Task CreateNetListJsonAsync(IProjectFile verilog)
    {
        await childProcessService.ExecuteShellAsync("yosys", [
                "-p", "hierarchy -auto-top; proc; opt; memory -nomap; wreduce -memx; opt_clean", "-o",
                $"{verilog.Header}.json", verilog.Header
            ],
            Path.GetDirectoryName(verilog.FullPath)!, "Create Netlist...");
    }
}