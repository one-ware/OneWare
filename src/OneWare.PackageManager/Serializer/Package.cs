namespace OneWare.PackageManager.Serializer;

public class Package
{
    public string? Type { get; init; }
    public string? Name { get; init; }
    public string? Id { get; init; }
    public string? License { get; init; }
    public string? LicenseUrl { get; init; }
    public string? IconUrl { get; init; }
    public PackageVersion[]? Versions { get; init; }
}