using System.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;

namespace OneWare.Essentials.Models;

public interface IPackageState : INotifyPropertyChanged
{
    Package Package { get; }
    
    PackageVersion? InstalledVersion { get; }

    string? InstalledVersionWarningText { get; }
    
    PackageStatus Status { get; }

    public bool IsIndeterminate { get; }

    public float Progress { get; }
}