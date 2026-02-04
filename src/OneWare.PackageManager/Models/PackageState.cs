using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Models;

public class PackageState(Package package) : ObservableObject, IPackageState
{
    public Package Package
    {
        get;
        internal set => SetProperty(ref field, value);
    } = package;

    public PackageVersion? InstalledVersion
    {
        get;
        internal set => SetProperty(ref field, value);
    }

    public string? InstalledVersionWarningText
    {
        get;
        internal set => SetProperty(ref field, value);
    }

    public PackageStatus Status
    {
        get;
        internal set => SetProperty(ref field, value);
    }

    public bool IsIndeterminate
    {
        get;
        internal set => SetProperty(ref field, value);
    }

    public float Progress
    {
        get;
        internal set => SetProperty(ref field, value);
    }
}
