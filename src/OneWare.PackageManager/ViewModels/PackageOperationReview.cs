using Avalonia.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.ViewModels;

/// <summary>Shared review for details, quick install and joint Update All; independent of loaded tabs.</summary>
public static class PackageOperationReview
{
    public static async Task<PackageInstallResult> RunAsync(IPackageService packages, IWindowService windows,
        IReadOnlyList<PackageRequest> roots, Window? owner = null, CancellationToken cancellationToken = default)
    {
        PackageInstallResult result;
        try
        {
            result = await ReviewAsync(packages, windows, roots, owner, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = new() { Status = PackageInstallResultReason.Cancelled, Message = "Package changes cancelled." };
        }
        catch (Exception ex)
        {
            result = new() { Status = PackageInstallResultReason.InvalidPlan, Message = ex.Message };
        }

        if (result.Status is not (PackageInstallResultReason.Installed or PackageInstallResultReason.AlreadyInstalled)
            && (result.Status != PackageInstallResultReason.Cancelled || result.CompletedPackages.Count > 0))
            await windows.ShowMessageAsync("Package changes not completed", DescribeResult(result), MessageBoxIcon.Warning, owner);
        return result;
    }

    public static string DescribeResult(PackageInstallResult result) =>
        (result.Message ?? result.Status.ToString()) +
        (result.CompatibilityRecord == null ? "" : "\n" + result.CompatibilityRecord.Report) +
        (result.CompletedPackages.Count == 0 ? "" : "\nCompleted and retained: " + string.Join(", ", result.CompletedPackages)) +
        (result.RestartRequired ? "\nRestart required to apply plugin changes." : "");

    private static async Task<PackageInstallResult> ReviewAsync(IPackageService packages, IWindowService windows,
        IReadOnlyList<PackageRequest> roots, Window? owner, CancellationToken cancellationToken)
    {
        if (packages is not IPackageOperationService operations)
            return new() { Status = PackageInstallResultReason.InvalidPlan, Message = "Package planning is unavailable." };
        var plan = await operations.PlanAsync(roots, cancellationToken);
        if (!plan.IsValid)
            return new() { Status = PackageInstallResultReason.InvalidPlan, Message = string.Join("\n", plan.Errors) };
        cancellationToken.ThrowIfCancellationRequested();
        if (plan.Items.All(x => x.Action == PackagePlanAction.Reuse))
            return await operations.ExecuteAsync(plan, [], cancellationToken);
        var message = string.Join("\n", plan.Items.Select(item =>
            $"- **{item.Name}**: {item.Action} {item.InstalledVersion ?? "not installed"} → {item.Version}" +
            (roots.Any(r => r.Id == item.Id) ? "" : " — required by " + string.Join(", ", plan.Items.Where(p => p.Dependencies.Any(d => d.Id == item.Id)).Select(p => p.Name)))));
        var accepted = new List<string>();
        foreach (var item in plan.Items.Where(x => x.RequiresLicense))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!packages.Packages.TryGetValue(item.Id, out var state))
                return new() { Status = PackageInstallResultReason.PlanChanged, Message = "Packages changed. Review the installation again." };
            var license = await packages.DownloadLicenseAsync(state.Package);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(license))
            {
                return new() { Status = PackageInstallResultReason.ConsentRequired,
                    Message = $"The required license for {item.Name} could not be loaded. No packages were changed." };
            }
            message += $"\n\n## {item.Name} — License\n\n{license}";
            accepted.Add(item.Id);
        }
        var response = await windows.ShowMessageBoxAsync(new MessageBoxRequest
        {
            Title = "Review package changes",
            Message = message + "\n\nUnused dependencies are retained. Plugin updates require restart.",
            Icon = MessageBoxIcon.Info,
            Buttons = [new MessageBoxButton { Text = accepted.Count > 0 ? "Accept licenses and install" : "Install changes",
                Role = MessageBoxButtonRole.Yes, Style = MessageBoxButtonStyle.Primary, IsDefault = true },
                new MessageBoxButton { Text = "Cancel", Role = MessageBoxButtonRole.Cancel, Style = MessageBoxButtonStyle.Secondary }]
        }, owner);
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsAccepted) return new() { Status = PackageInstallResultReason.Cancelled };
        return await operations.ExecuteAsync(plan, accepted, cancellationToken);
    }
}