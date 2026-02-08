using System.Text.Json.Nodes;

namespace OneWare.ProjectSystem;

public class UniversalProjectProperties
{
    public Dictionary<string, object> Settings { get; } = new();
}