using System.Text.Json;

namespace OneWare.Essentials.PackageManager;

public class PackageManifest
{
    public string? ManifestUrl { get; init; }

    public JsonElement? Content { get; init; }
    
    public string? Icon { get; init; }
}