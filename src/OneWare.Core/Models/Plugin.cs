using OneWare.SDK.Models;

namespace OneWare.Core.Models;

public class Plugin : IPlugin
{
    public string Id { get; }
    
    public string Path { get; }

    public bool IsCompatible { get; set; }
    
    public string? CompatibilityReport { get; set; }

    public Plugin(string id, string path)
    {
        Id = id;
        Path = path;
    }
}