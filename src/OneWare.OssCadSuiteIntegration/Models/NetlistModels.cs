using System.Text.Json.Serialization;

namespace OneWare.OssCadSuiteIntegration.Models;

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