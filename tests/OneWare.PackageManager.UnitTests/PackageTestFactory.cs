using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.ViewModels;

namespace OneWare.PackageManager.UnitTests;

/// <summary>
///     Hand written because NSubstitute cannot stub the <c>ResolveTargetVersion</c> extension method and
///     <c>OneWare.PackageManager.Models.PackageState</c> only exposes internal setters.
/// </summary>
internal sealed class FakeState : INotifyPropertyChanged, IPackageState
{
    private PackageStatus _status;

    public required Package Package { get; init; }
    public PackageVersion? InstalledVersion { get; set; }
    public string? InstalledVersionWarningText => null;
    public bool IsIndeterminate => false;
    public float Progress => 0;

    public PackageStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal static class PackageTestFactory
{
    public static PackageViewModel CreateViewModel(string id, string name, PackageStatus status)
    {
        var pkg = new Package { Id = id, Name = name, Versions = [new PackageVersion { Version = "1.0.0" }] };
        var state = new FakeState { Package = pkg, Status = status };
        if (status != PackageStatus.Available) state.InstalledVersion = pkg.Versions![0];

        return new PackageViewModel(state, Substitute.For<IPackageService>(), Substitute.For<IHttpService>(),
            Substitute.For<IWindowService>(), Substitute.For<IApplicationStateService>(),
            Substitute.For<ILogger>());
    }
}
