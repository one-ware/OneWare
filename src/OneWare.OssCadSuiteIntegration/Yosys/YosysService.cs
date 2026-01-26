using System.Text.Json;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using OneWare.OssCadSuiteIntegration.Models;
using OneWare.ToolEngine.Services;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using Microsoft.Extensions.Logging;

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysService(
    IChildProcessService childProcessService,
    ILogger logger,
    IOutputService outputService,
    IMainDockService mainDockService, 
    IToolService toolService,
    IToolExecutionDispatcherService toolExecutionDispatcherService)
{

    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        return await CompileAsync(project, fpgaModel, null);
    }
    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel, IEnumerable<string>? mandatoryFiles)
    {
        var buildDir = Path.Combine(project.FullPath, "build");
        Directory.CreateDirectory(buildDir);

        mainDockService.Show<IOutputService>();

        var start = DateTime.Now;
            
        outputService.WriteLine("Compiling...\n==================");
        
        var success = await SynthAsync(project, fpgaModel, mandatoryFiles);
        success = success && await FitAsync(project, fpgaModel);
        success = success && await AssembleAsync(project, fpgaModel);
        
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

    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        return await SynthAsync(project, fpgaModel, null);
    }
    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel, IEnumerable<string>? mandatoryFiles) 
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
            
            var yosysSynthTool = properties.GetValueOrDefault("yosysToolchainYosysSynthTool") ??
                                 throw new Exception("Yosys Tool not set!");

            List<string> yosysArguments =
                ["-q", "-p", $"{yosysSynthTool} -json build/synth.json"];
            
            yosysArguments.AddRange(properties.GetValueOrDefault("yosysToolchainYosysFlags")?.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);
            
            yosysArguments.AddRange(includedFiles);
            
            
            yosysArguments.AddRange(mandatoryFiles ?? []);

            var command = ToolCommand.FromShellParams("yosys", yosysArguments, project.FullPath,
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
            
            var (success, _) = await toolExecutionDispatcherService.ExecuteAsync(command);
            
            return success;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }

    public Task<bool> FitAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
        => RunNextpnrAsync(project, fpgaModel, withGui: false);

    public Task<bool> OpenNextpnrGui(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
        => RunNextpnrAsync(project, fpgaModel, withGui: true);
    
    private async Task<bool> RunNextpnrAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel, bool withGui)
    {
        var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);

        var nextPnrTool = properties.GetValueOrDefault("yosysToolchainNextPnrTool")
                          ?? throw new Exception("NextPnr Tool not set!");

        var pcfFile = YosysSettingHelper.GetConstraintFile(project);
        
        var nextPnrArguments = new List<string>
        {
            "--json", "./build/synth.json",
            "--pcf", pcfFile,
            "--asc", "./build/nextpnr.asc"
        };

        if (withGui)
            nextPnrArguments.Add("--gui");

        nextPnrArguments.AddRange(properties
                                      .GetValueOrDefault("yosysToolchainNextPnrFlags")?
                                      .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                  ?? Array.Empty<string>());
        
        var command = ToolCommand.FromShellParams(nextPnrTool, nextPnrArguments,
            project.FullPath, $"Running {nextPnrTool}...", AppState.Loading, true, null, s =>
            {
                Dispatcher.UIThread.Post(() => { outputService.WriteLine(s); });
                return true;
            });
        
        var status = await toolExecutionDispatcherService.ExecuteAsync(command);
        
        return status.success;
    }

    public async Task<bool> AssembleAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);
        
        var packTool = properties.GetValueOrDefault("yosysToolchainPackTool") ??
                       throw new Exception("Pack Tool not set!");
        ;
        List<string> packToolArguments = ["./build/nextpnr.asc", "./build/pack.bin"];
        packToolArguments.AddRange(properties.GetValueOrDefault("yosysToolchainPackFlags")?.Split(' ',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);
        
        var status = await childProcessService.ExecuteShellAsync(packTool, packToolArguments,
            project.FullPath,
            $"Running {packTool}...");

        return status.success;
    }

    [Obsolete (message: "Use CreateJsonNetListAsync instead")]
    public async Task CreateNetListJsonAsync(IProjectFile verilog)
    {
        await childProcessService.ExecuteShellAsync("yosys", [
                "-p", "hierarchy -auto-top; proc; opt; memory -nomap; wreduce -memx; opt_clean", "-o",
                $"{verilog.Header}.json", verilog.Header
            ],
            Path.GetDirectoryName(verilog.FullPath)!, "Create Netlist...");
    }
    
    public async Task<bool> CreateJsonNetListAsync(IProjectFile verilog)
    {
        var result = await childProcessService.ExecuteShellAsync("yosys", [
                "-p", "hierarchy -auto-top; proc; opt; memory -nomap; wreduce -memx; opt_clean", "-o",
                $"{verilog.Header}.json", verilog.Header
            ],
            Path.GetDirectoryName(verilog.FullPath)!, "Create Netlist...");
        
        return result.success;
    }

    public async Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        var buildpath = Path.Combine(file.Root.FullPath, "build");
        Directory.CreateDirectory(buildpath);
        await childProcessService.ExecuteShellAsync("yosys", ["-p", $"read_verilog {file.RelativePath}; proc; write_json build/yosys_nodes.json"],
            file.Root.FullPath, "Running Yosys...", AppState.Loading, true);
        return ReadJson(Path.Combine(buildpath, "yosys_nodes.json"));
    }
    
    private List<FpgaNode> ReadJson(string filePath)
    {
        try
        {
            var jsonString = File.ReadAllText(filePath);
            
            var yosysData = JsonSerializer.Deserialize<YosysOutput>(jsonString);

            if (yosysData != null && yosysData.Modules.Count > 0)
            {
                List<FpgaNode> nodes = [];
                var firstModule = yosysData.Modules.Values.First();

                foreach (var portEntry in firstModule.Ports)
                {
                    var baseName = portEntry.Key;
                    var port = portEntry.Value;
                    var width = port.Bits.Count; 
                    
                    if (width > 1)
                    {
                        for (int i = 0; i < width; i++)
                        {
                            string vectorName = $"{baseName}[{i}]";
                            nodes.Add(new FpgaNode(vectorName, port.Direction));
                        }
                    }
                    else
                    {
                        nodes.Add(new FpgaNode(baseName, port.Direction));
                    }
                }

                return nodes;
            }
        }
        catch (FileNotFoundException)
        {
            logger.Error($"The file '{filePath} could not be found.");
        }
        catch (JsonException)
        {
            logger.Error("The Yosys output didn't contain a valid JSON file.");
        }
        catch (Exception ex)
        {
            logger.Error($"There is a unkown error: {ex.Message}");
        }

        return [];
    }
}