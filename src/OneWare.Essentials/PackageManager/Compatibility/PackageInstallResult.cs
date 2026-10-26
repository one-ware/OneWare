namespace OneWare.Essentials.PackageManager.Compatibility;

public enum PackageInstallResultReason
{
    Installed,
    AlreadyInstalled,
    NotFound,
    ErrorDownloading,
    Incompatible,
    MissingDependency
}

public class PackageInstallResult
{
    public PackageInstallResultReason Status { get; init; }
    
    public CompatibilityReport? CompatibilityRecord { get; init; }
}