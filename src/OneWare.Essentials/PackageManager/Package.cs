namespace OneWare.Essentials.PackageManager;

public class Package
{
    public string? Category { get; init; }

    public string? Type { get; init; }

    public string? Name { get; init; }

    public string? Id { get; init; }

    public string? Description { get; init; }

    public string? License { get; init; }

    public bool AcceptLicenseBeforeDownload { get; init; }

    public string? UrlLaunchIds { get; init; }

    public string? IconUrl { get; init; }

    public string? SourceUrl { get; init; }

    public PackageTab[]? Tabs { get; init; }

    public PackageLink[]? Links { get; init; }

    public PackageVersion[]? Versions { get; init; }
}