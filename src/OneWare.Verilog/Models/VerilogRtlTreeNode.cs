using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace OneWare.Verilog.Models;

public class VerilogRtlTreeNode
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("inst")]
    public string Instance { get; set; } = string.Empty;

    [JsonProperty("file")]
    public string File { get; set; } = string.Empty;

    [JsonProperty("line")]
    public int Line { get; set; }

    [JsonProperty("col")]
    public int Column { get; set; }

    [JsonProperty("recursive")]
    public bool IsRecursive { get; set; }

    [JsonProperty("truncated")]
    public bool IsTruncated { get; set; }

    [JsonProperty("children")]
    public ObservableCollection<VerilogRtlTreeNode> Children { get; set; } = new();

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Instance) ? Name : $"{Instance} : {Name}";
}
