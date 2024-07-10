namespace OneWare.Essentials.PackageManager;

public class PackageVersion
{
    public string? Version { get; init; }
    
    public PackageTarget[]? Targets { get; init; }
}