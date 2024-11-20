namespace OneWare.Essentials.PackageManager;

public class PackageVersion
{
    public string? Version { get; init; }
    
    public string? MinStudioVersion { get; init; }

    public PackageTarget[]? Targets { get; init; }
    
    public string? CompatibilityUrl { get; init; }
}