using OneWare.Essentials.Models;

namespace OneWare.Core.Models;

public class Plugin : IPlugin
{
    public Plugin(string id, string path)
    {
        Id = id;
        Path = path;
    }

    public string Id { get; }

    public string Path { get; }

    public bool IsCompatible { get; set; }

    public string? CompatibilityReport { get; set; }
}