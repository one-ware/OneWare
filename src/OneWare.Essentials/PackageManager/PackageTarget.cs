namespace OneWare.Essentials.PackageManager;

public class PackageTarget
{
    public string? Target { get; init; }
    
    public string? Url { get; init; }
    
    public PackageAutoSetting[]? AutoSetting { get; init; }
}