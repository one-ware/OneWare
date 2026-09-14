namespace OneWare.Essentials.PackageManager.Compatibility;

public enum PackageInstallResultReason
{
    Installed,
    AlreadyInstalled,
    NotFound,
    ErrorDownloading,
    Incompatible,
    ConsentRequired,
    InvalidPlan,
    PlanChanged,
    Cancelled
}

public class PackageInstallResult
{
    public PackageInstallResultReason Status { get; init; }
    
    public CompatibilityReport? CompatibilityRecord { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> CompletedPackages { get; init; } = [];
    public bool RestartRequired { get; init; }
}