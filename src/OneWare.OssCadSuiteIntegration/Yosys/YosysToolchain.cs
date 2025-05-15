using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OneWare.OssCadSuiteIntegration.Yosys
{
    public class YosysToolchain : IFpgaToolchain
    {
        private readonly YosysService _yosysService;
        private readonly ILogger<YosysToolchain> _logger;

        // Constructor injection for dependencies
        public YosysToolchain(YosysService yosysService, ILogger<YosysToolchain> logger)
        {
            _yosysService = yosysService;
            _logger = logger;
        }

        public string Name => "Yosys";

        public void OnProjectCreated(UniversalFpgaProjectRoot project)
        {
            // No implementation provided, assuming it's handled elsewhere
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
                                _logger.LogWarning("PCF Line invalid: " + trimmedLine);
                                continue;
                            }

                            var signal = parts[1];
                            var pin = parts[2];

                            if (fpga.PinModels.TryGetValue(pin, out var pinModel) &&
                                fpga.NodeModels.TryGetValue(signal, out var signalModel))
                            {
                                fpga.Connect(pinModel, signalModel);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
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

                foreach (var conn in fpga.PinModels.Where(x => x.Value.ConnectedNode != null))
                {
                    pcf += $"\nset_io {conn.Value.ConnectedNode!.Node.Name} {conn.Value.Pin.Name}";
                }
                pcf = pcf.Trim() + '\n';

                File.WriteAllText(pcfPath, pcf);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }

        public Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
        {
            return _yosysService.CompileAsync(project, fpga);
        }

        public Task<bool> SynthesisAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
        {
            return _yosysService.SynthAsync(project, fpga);
        }

        public Task<bool> FitAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
        {
            return _yosysService.FitAsync(project, fpga);
        }

        public Task<bool> AssembleAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
        {
            return _yosysService.AssembleAsync(project, fpga);
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
}
