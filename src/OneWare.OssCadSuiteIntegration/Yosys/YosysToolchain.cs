using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.OssCadSuiteIntegration.Yosys;

public class YosysToolchain : IFpgaToolchain
{
    private readonly YosysService _yosysService;
    public string Name => "Yosys";

    public YosysToolchain(YosysService yosysService)
    {
        _yosysService = yosysService;
    }
    
    public void LoadConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        try
        {
            var files = Directory.GetFiles(project.RootFolderPath);
            var pcfPath = files.FirstOrDefault(x => Path.GetExtension(x) == ".pcf");
            if (pcfPath != null)
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

                        if (fpga.PinModels.TryGetValue(pin, out var pinModel) && fpga.NodeModels.TryGetValue(signal, out var signalModel))
                        {
                            fpga.Connect(pinModel, signalModel);
                        } 
                    }
                }
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var pcfPath = Path.Combine(project.FullPath, "project.pcf");

        try
        {
            var pcf = "";
            if (File.Exists(pcfPath))
            {
                var existingPcf = File.ReadAllText(pcfPath);
                existingPcf = RemoveLine(existingPcf, "set_io");
                pcf = existingPcf.Trim();
            }

            foreach (var conn in fpga.PinModels.Where(x => x.Value.Connection is not null))
            {
                pcf += $"\nset_io {conn.Value.Connection!.Node.Name} {conn.Value.Pin.Name}";
            }
            pcf = pcf.Trim() + '\n';
            
            File.WriteAllText(pcfPath, pcf);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public void StartCompile(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        _yosysService.SynthAsync(project);
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