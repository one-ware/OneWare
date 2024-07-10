namespace OneWare.Essentials.PackageManager;

public class InstalledPackage(
    string id,
    string type,
    string name,
    string? category,
    string? description,
    string? license,
    string installedVersion)
{
    public string Id { get; } = id;
    
    public string Type { get; } = type;

    public string Name { get; } = name;
    
    public string? Category { get; } = category;

    public string? Description { get; } = description;

    public string? License { get; } = license;

    public string InstalledVersion { get; } = installedVersion;
}