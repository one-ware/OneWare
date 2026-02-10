using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Tools;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysToolchain(YosysService yosysService) : IFpgaToolchain
{
    public const string ToolChainId = "yosys";

    public string Id => ToolChainId;
    
    public virtual string Name => "Yosys";

    public virtual void OnProjectCreated(UniversalFpgaProjectRoot project)
    {
        YosysSettingHelper.UpdateProjectProperties(project, null);
    }

    public virtual void LoadConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        try
        {
            var pcfPath = Path.Combine(project.FullPath, YosysSettingHelper.GetConstraintFile(project));
            if (File.Exists(pcfPath))
            {
                var pcf = File.ReadAllText(pcfPath);
                var lines = pcf.Split('\n');
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("set_io"))
                    {
                        var parts = trimmedLine.Split(' ');
                        if (parts.Length != 3)
                        {
                            ContainerLocator.Container.Resolve<ILogger>().Warning("PCF Line invalid: " + trimmedLine);
                            continue;
                        }

                        var signal = parts[1];
                        var pin = parts[2];

                        if (fpga.PinModels.TryGetValue(pin, out var pinModel) &&
                            fpga.NodeModels.TryGetValue(signal, out var signalModel))
                            fpga.Connect(pinModel, signalModel);
                    }
                }
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public virtual void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var pcfPath = Path.Combine(project.FullPath, YosysSettingHelper.GetConstraintFile(project));

        try
        {
            var pcf = "";
            if (File.Exists(pcfPath))
            {
                var existingPcf = File.ReadAllText(pcfPath);
                existingPcf = RemoveLine(existingPcf, "set_io");
                pcf = existingPcf.Trim();
            }

            foreach (var conn in fpga.PinModels.Where(x => x.Value.ConnectedNode is not null))
                pcf += $"\nset_io {conn.Value.ConnectedNode!.Node.Name} {conn.Value.Pin.Name}";
            pcf = pcf.Trim() + '\n';

            File.WriteAllText(pcfPath, pcf);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public virtual Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return yosysService.CompileAsync(project, fpga);
    }

    public virtual Task<bool> SynthesisAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return yosysService.SynthAsync(project, fpga);
    }

    public virtual Task<bool> FitAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return yosysService.FitAsync(project, fpga);
    }

    public virtual Task<bool> AssembleAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return yosysService.AssembleAsync(project, fpga);
    }

    private string RemoveLine(string file, string find)
    {
        var startIndex = file.IndexOf(find, StringComparison.Ordinal);
        while (startIndex > -1)
        {
            var endIndex = file.IndexOf('\n', startIndex);
            if (endIndex == -1) endIndex = file.Length - 1;
            file = file.Remove(startIndex, endIndex - startIndex + 1);
            startIndex = file.IndexOf(find, startIndex, StringComparison.Ordinal);
        }

        return file;
    }
}