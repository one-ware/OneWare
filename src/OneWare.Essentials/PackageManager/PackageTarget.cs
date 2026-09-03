namespace OneWare.Essentials.PackageManager;

public class PackageTarget
{
    public string? Target { get; init; }

    public string? Url { get; init; }

    public bool IsArchive { get; init; } = true;

    public PackageAutoSetting[]? AutoSetting { get; init; }
}