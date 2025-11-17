using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text.Json;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Tools;

public class YosysNodeProvider(IChildProcessService childProcess): INodeProvider
{
  
   public async Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        await childProcess.ExecuteShellAsync("yosys", ["-p", $"read_verilog {file.RelativePath}; proc; write_json build/yosys_nodes.json"],
            file.Root.FullPath, "Running Yosys...", AppState.Loading, true);
        return ReadJson(Path.Combine(file.Root.FullPath, "build/yosys_nodes.json"));
    }

    public string GetDisplayName()
    {
        return "Yosys NodeProvider";
    }

    public string GetKey()
    {
        return "YosysNodeProvider";
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
                var moduleName = yosysData.Modules.Keys.First();
                // TODO: Impl Concept for Vectors. 

                Console.WriteLine($"Modul: {moduleName}");
                
                foreach (var portEntry in firstModule.Ports)
                {
                    Console.WriteLine($"  Port: {portEntry.Key}");
                    Console.WriteLine($"    Direction: {portEntry.Value.Direction}");
                    Console.WriteLine($"    Bits: {string.Join(", ", portEntry.Value.Bits)}");
                    nodes.Add(new FpgaNode(portEntry.Key, portEntry.Value.Direction));
                }

                return nodes;
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Fehler: Die Datei '{filePath}' wurde nicht gefunden.");
        }
        catch (JsonException)
        {
            Console.WriteLine("Fehler: Die Datei enthält ungültiges JSON.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ein unerwarteter Fehler ist aufgetreten: {ex.Message}");
        }

        return new List<FpgaNode>();
    }
}



public class YosysOutput
{
    [JsonPropertyName("creator")] public string Creator { get; set; } = string.Empty;

    [JsonPropertyName("modules")] public Dictionary<string, YosysModule> Modules { get; set; } = null!;
}

public class YosysModule
{
    [JsonPropertyName("attributes")] public Dictionary<string, string> Attributes { get; set; } = null!;

    [JsonPropertyName("ports")] public Dictionary<string, YosysPort> Ports { get; set; } = null!;

}

public class YosysPort
{
    [JsonPropertyName("direction")] public string Direction { get; set; } = null!;

    [JsonPropertyName("bits")] public List<int> Bits { get; set; } = null!;
}