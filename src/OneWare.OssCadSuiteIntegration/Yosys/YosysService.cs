using System.Text.Json;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
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

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysService(
    IChildProcessService childProcessService,
    ILogger logger,
    IOutputService outputService,
    IDockService dockService, 
    ToolService toolService,
    ToolExecutionDispatcherService toolExecutionDispatcherService)
{

    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        return await CompileAsync(project, fpgaModel, null);
    }
    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel, IEnumerable<string>? mandatoryFiles)
    {
        var buildDir = Path.Combine(project.FullPath, "build");
        Directory.CreateDirectory(buildDir);

        dockService.Show<IOutputService>();

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

            var yosysCommand = properties.GetValueOrDefault("yosysToolchainCommand") ?? "";
            var yosysQuiet = Boolean.Parse(properties.GetValueOrDefault("yosysQuietFlag") ?? "true");
            List<string> yosysArguments = [];
            
            if (yosysQuiet)
                yosysArguments.Add("-q");
            
            if (string.IsNullOrWhiteSpace(yosysCommand))
            {
                yosysArguments.AddRange( ["-p", $"{yosysSynthTool} -json build/synth.json"]);
            }
            else
            {
                yosysCommand = yosysCommand.Replace("$TOP", top.Split(".")[0]);
                yosysCommand = yosysCommand.Replace("$SYNTH_TOOL", yosysSynthTool);
                yosysCommand = yosysCommand.Replace("$OUTPUT", "build/synth.json");
                
                yosysArguments.AddRange([ "-p", yosysCommand]);
            }
            
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

    public Task<bool> OpenNextpnrGuiAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
        => RunNextpnrAsync(project, fpgaModel, withGui: true);
    
    private async Task<bool> RunNextpnrAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel, bool withGui)
    {
        var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);

        var nextPnrTool = properties.GetValueOrDefault("yosysToolchainNextPnrTool")
                          ?? throw new Exception("NextPnr Tool not set!");

        var pcfFile = YosysSettingHelper.GetConstraintFile(project);
        var cFileType = properties
            .GetValueOrDefault("yosysToolchainConstraintFileType", "pcf");
        
        
        
        var nextPnrArguments = new List<string>
        {
            "--json", "./build/synth.json",
        };

        switch (cFileType)
        {
            case "pcf":
                nextPnrArguments.Add("--pcf");
                nextPnrArguments.Add(pcfFile);
                break;
            case "ccf":
                var absolutePcfPath = Path.Combine(project.RootFolderPath, pcfFile);
                outputService.WriteLine($"Converting {absolutePcfPath} to CCF File");
                if(Path.Exists(absolutePcfPath))
                    ConstraintFileHelper.Convert(absolutePcfPath, Path.Combine(project.RootFolderPath, 
                        "./build/constrains.ccf"));
                else
                {
                    outputService.WriteLine($"Could not generate CCF file from {pcfFile}");
                    return false;
                } 
                    
                nextPnrArguments.Add("-o");
                nextPnrArguments.Add($"ccf=./build/constrains.ccf");
                break;
            default:
                outputService.WriteLine($"Could not find Constraint file type: {cFileType}");
                return false;
        }
        
        var cOutputType = properties
            .GetValueOrDefault("yosysToolchainOutputType", "asc");
        
        switch (cOutputType)
        {
            case "asc":
                nextPnrArguments.Add("--asc");
                nextPnrArguments.Add("./build/nextpnr.asc");
                break;
            case "txt":
                nextPnrArguments.Add("-o");
                nextPnrArguments.Add("out=./build/impl.txt");
                break;
            default:
                outputService.WriteLine($"Could not find output type: {cOutputType}");
                return false;
        }
        

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
        List<string> packToolArguments = [];
        
        var cOutputType = properties
            .GetValueOrDefault("yosysToolchainOutputType", "asc");
        
        switch (cOutputType)
        {
            case "asc":
                packToolArguments.Add("./build/nextpnr.asc");
                break;
            case "txt":
                packToolArguments.Add("./build/impl.txt");
                break;
            default:
                outputService.WriteLine($"Could not find input type: {cOutputType}");
                return false;
        }
        
        var pOutputFormat = properties
            .GetValueOrDefault("packToolOutputFormat", "bin");
        
        switch (pOutputFormat)
        {
            case "bin":
                packToolArguments.Add("./build/pack.bin");
                break;
            case "bit":
                packToolArguments.Add("./build/pack.bit");
                break;
            default:
                outputService.WriteLine($"Could not find output type: {pOutputFormat}");
                return false;
        }
        
        
        packToolArguments.AddRange(properties.GetValueOrDefault("yosysToolchainPackFlags")?.Split(' ',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);
        
        var command = ToolCommand.FromShellParams(packTool, packToolArguments,
            project.FullPath, $"Running {packTool}...", AppState.Loading, true, null, s =>
            {
                Dispatcher.UIThread.Post(() => { outputService.WriteLine(s); });
                return true;
            });
        
        var status = await toolExecutionDispatcherService.ExecuteAsync(command);
        
        return status.success;
    }

    [Obsolete (message: "Use CreateJsonNetListAsync instead")]
    public async Task CreateNetListJsonAsync(IProjectFile verilog)
    {
        
        var command = ToolCommand.FromShellParams("yosys", [
                "-p", "hierarchy -auto-top; proc; opt; memory -nomap; wreduce -memx; opt_clean", "-o",
                $"{verilog.Header}.json", verilog.Header
            ],
            Path.GetDirectoryName(verilog.FullPath)!, $"Create Netlist...");
        
        await toolExecutionDispatcherService.ExecuteAsync(command);
        
    }

    public async Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        var buildpath = Path.Combine(file.Root.FullPath, "build");
        Directory.CreateDirectory(buildpath);
        
        var command = ToolCommand.FromShellParams("yosys",  ["-p", $"read_verilog {file.RelativePath}; proc; write_json build/yosys_nodes.json"],
            file.Root.FullPath, $"Running Yosys...", AppState.Loading, true);
        await toolExecutionDispatcherService.ExecuteAsync(command);
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