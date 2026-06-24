using System.Text.Json;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ToolEngine;
using OneWare.OssCadSuiteIntegration.Models;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysService(
    ILogger logger,
    IOutputService outputService,
    IMainDockService dockService,
    IToolExecutionDispatcherService toolExecutionDispatcherService)
{
    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        return await CompileAsync(project, fpgaModel, null);
    }

    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel,
        IEnumerable<string>? mandatoryFiles)
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

    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel, IEnumerable<string>? mandatoryFiles = null)
    {
        try
        {
            var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);
            var top = project.TopEntity ?? throw new Exception("TopEntity not set!");

            var includedFiles = project.GetFiles("*.v").Concat(project.GetFiles("*.sv"))
                .Where(x => !project.IsCompileExcluded(x))
                .Where(x => !project.IsTestBench(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var genVerilogPath = Path.Combine(project.RootFolderPath, "build", "gen_verilog");
            if (Directory.Exists(genVerilogPath))
            {
                foreach (var absFile in Directory.EnumerateFiles(genVerilogPath, "*.v", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(project.RootFolderPath, absFile);
                    includedFiles.Add(rel);
                }
            }
            
            var yosysSynthTool = properties.GetValueOrDefault("yosysToolchainYosysSynthTool") ??
                                 throw new Exception("Yosys Tool not set. This hardware might not be configured to be used with Yosys Toolchain");
            
            var builder = toolExecutionDispatcherService.CreateToolCommandBuilder("yosys")
                .WithWorkingDirectory(project.FullPath)
                .WithStatus("Running yosys...")
                .WithTimer(true)
                .WithOutputHandler(x =>
                {
                    if (x.StartsWith("Error:"))
                    {
                        logger.Error(x);
                        return false;
                    }

                    outputService.WriteLine(x);
                    return true;
                })
                .WithErrorHandler(x =>
                {
                    if (x.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Error(x);
                        return false;
                    }

                    outputService.WriteLine(x);
                    return true;
                });


            bool.TryParse(properties.GetValueOrDefault("yosysQuietFlag") ?? "true", out var quiet);
            builder.AddIf(quiet, "-q");
            
            var customCommandTemplate = properties.GetValueOrDefault("yosysToolchainCommand");
            builder.Add("-p");

            if (string.IsNullOrWhiteSpace(customCommandTemplate))
            {
                builder.AddScript("{synthTool} -json {output}",
                    ("{synthTool}", yosysSynthTool, false), 
                    ("{output}", "build/synth.json", true)  
                );
            }
            else
            {
                builder.AddScript(customCommandTemplate,
                    ("$TOP", top, false), 
                    ("$SYNTH_TOOL", yosysSynthTool, false), 
                    ("$OUTPUT", "build/synth.json", true)   
                );
            }
            
            builder.AddRawArguments(properties.GetValueOrDefault("yosysToolchainYosysFlags"));
            builder.AddPaths(includedFiles);

            if (mandatoryFiles != null)
            {
                builder.AddPaths(mandatoryFiles);
            }

            var command = builder.Build();

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
        try
        {
            var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);
            var nextPnrTool = properties.GetValueOrDefault("yosysToolchainNextPnrTool")
                              ?? throw new Exception("NextPnr Tool not set!");

            var builder = toolExecutionDispatcherService.CreateToolCommandBuilder(nextPnrTool)
                .WithWorkingDirectory(project.FullPath)
                .WithStatus($"Running {nextPnrTool}...")
                .WithTimer(true)
                .WithErrorHandler(s =>
                {
                    Dispatcher.UIThread.Post(() => { outputService.WriteLine(s); });
                    return true;
                });
            
            builder.AddPathOption("--json", "./build/synth.json");

            var pcfFile = YosysSettingHelper.GetConstraintFile(project);
            var cFileType = properties.GetValueOrDefault("yosysToolchainConstraintFileType", "pcf");

            switch (cFileType)
            {
                case "pcf":
                    builder.Add("--pcf").AddPath(pcfFile);
                    break;
                case "ccf":
                    var absolutePcfPath = Path.Combine(project.RootFolderPath, pcfFile);
                    outputService.WriteLine($"Converting {absolutePcfPath} to CCF File");

                    if (File.Exists(absolutePcfPath))
                    {
                        ConstraintFileHelper.Convert(absolutePcfPath,
                            Path.Combine(project.RootFolderPath, "./build/constrains.ccf"));
                    }
                    else
                    {
                        outputService.WriteLine($"Could not generate CCF file from {pcfFile}");
                        return false;
                    }

                    builder.Add("-o");
                    builder.AddScript("ccf={path}", ("{path}", "./build/constrains.ccf"));
                    break;
                default:
                    outputService.WriteLine($"Could not find Constraint file type: {cFileType}");
                    return false;
            }

            var cOutputType = properties.GetValueOrDefault("yosysToolchainOutputType", "asc");
            switch (cOutputType)
            {
                case "asc":
                    builder.Add("--asc").AddPath("./build/nextpnr.asc");
                    break;
                case "txt":
                    builder.Add("-o");
                    builder.AddScript("out={path}", ("{path}", "./build/impl.txt"));
                    break;
                default:
                    outputService.WriteLine($"Could not find output type: {cOutputType}");
                    return false;
            }

            if (withGui) builder.Add("--gui");

            var extraFlags = properties.GetValueOrDefault("yosysToolchainNextPnrFlags")?
                .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

            foreach (var flag in extraFlags) builder.Add(flag);

            var command = builder.Build();
            var status = await toolExecutionDispatcherService.ExecuteAsync(command);
            return status.success;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }

    public async Task<bool> AssembleAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        try
        {
            var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);
            var packTool = properties.GetValueOrDefault("yosysToolchainPackTool")
                           ?? throw new Exception("Pack Tool not set!");

            var builder = toolExecutionDispatcherService.CreateToolCommandBuilder(packTool)
                .WithWorkingDirectory(project.FullPath)
                .WithStatus($"Running {packTool}...")
                .WithTimer(true)
                .WithErrorHandler(s =>
                {
                    Dispatcher.UIThread.Post(() => { outputService.WriteLine(s); });
                    return true;
                });

            var cOutputType = properties.GetValueOrDefault("yosysToolchainOutputType", "asc");
            string inputPath = cOutputType switch
            {
                "asc" => "./build/nextpnr.asc",
                "txt" => "./build/impl.txt",
                _ => throw new ArgumentException($"Unsupported input type: {cOutputType}")
            };
            builder.AddPath(inputPath);

            var pOutputFormat = properties.GetValueOrDefault("packToolOutputFormat", "bin");
            string outputPath = pOutputFormat switch
            {
                "bin" => "./build/pack.bin",
                "bit" => "./build/pack.bit",
                _ => throw new ArgumentException($"Unsupported output format: {pOutputFormat}")
            };
            builder.AddPath(outputPath);

            var flags = properties.GetValueOrDefault("yosysToolchainPackFlags")?.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

            foreach (var flag in flags)
            {
                builder.Add(flag);
            }

            var command = builder.Build();

            var status = await toolExecutionDispatcherService.ExecuteAsync(command);
            return status.success;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            outputService.WriteLine($"Error: {e.Message}");
            return false;
        }
    }

    [Obsolete(message: "Use CreateJsonNetListAsync instead")]
    public async Task CreateNetListJsonAsync(IProjectFile verilog)
    {
        var command = toolExecutionDispatcherService.CreateToolCommandBuilder("yosys")
            .WithWorkingDirectory(Path.GetDirectoryName(verilog.FullPath)!)
            .WithStatus("Create Netlist...")
            .Add("-p", "hierarchy -auto-top; proc; opt; memory -nomap; wreduce -memx; opt_clean")
            .Add("-o")
            .AddPath($"{verilog.Header}.json")
            .AddPath(verilog.Header)
            .Build();

        await toolExecutionDispatcherService.ExecuteAsync(command);
    }

    public async Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        var buildpath = Path.Combine(file.Root.FullPath, "build");
        Directory.CreateDirectory(buildpath);

        var command = toolExecutionDispatcherService.CreateToolCommandBuilder("yosys")
            .WithWorkingDirectory(file.Root.FullPath)
            .WithStatus("Running Yosys...")
            .WithTimer(true)
            .Add("-p", $"read_verilog {file.RelativePath}; proc; write_json build/yosys_nodes.json")
            .Build();

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