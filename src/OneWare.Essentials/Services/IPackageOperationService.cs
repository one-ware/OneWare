using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;

namespace OneWare.Essentials.Services;

public sealed record PackageRequest(string Id, string? Version = null, bool IncludePrerelease = false);
public enum PackagePlanAction { Reuse, Install, Update }
public sealed record PackagePlanItem(string Id, string Name, string Version, string? InstalledVersion,
    PackagePlanAction Action, bool RequiresLicense, IReadOnlyList<PackageDependency> Dependencies);
public sealed record PackageOperationPlan(string Fingerprint, IReadOnlyList<PackageRequest> Roots,
    IReadOnlyList<PackagePlanItem> Items, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Additive API: preview, gather consent, then execute the unchanged preview.</summary>
public interface IPackageOperationService
{
    Task<PackageOperationPlan> PlanAsync(IReadOnlyList<PackageRequest> roots, CancellationToken cancellationToken = default);
    Task<PackageInstallResult> ExecuteAsync(PackageOperationPlan plan, IReadOnlyCollection<string> acceptedLicenses,
        CancellationToken cancellationToken = default);
}