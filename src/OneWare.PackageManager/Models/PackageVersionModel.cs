using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;

namespace OneWare.PackageManager.Models;

public class PackageVersionModel(PackageVersion version) : ObservableObject
{
    private CompatibilityReport? _compatibilityReport;

    public PackageVersion Version { get; } = version;

    public bool TargetAll => Version.Targets is [{ Target: "all" }];

    public CompatibilityReport? CompatibilityReport
    {
        get => _compatibilityReport;
        set => SetProperty(ref _compatibilityReport, value);
    }
}