namespace OneWare.PackageManager.Serializer;

public class InstalledPackage(
    string id,
    string type,
    string? category,
    string? description,
    string? license,
    string installedVersion)
{
    public string Id { get; } = id;
    
    public string Type { get; } = type;
    
    public string? Category { get; } = category;

    public string? Description { get; } = description;

    public string? License { get; } = license;

    public string InstalledVersion { get; } = installedVersion;
}