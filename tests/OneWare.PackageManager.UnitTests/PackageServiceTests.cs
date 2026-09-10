using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Installers;
using OneWare.PackageManager.Services;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class PackageServiceTests
{
    private readonly IPackageCatalog _catalog = Substitute.For<IPackageCatalog>();
    private readonly IPackageDownloader _downloader = Substitute.For<IPackageDownloader>();
    private readonly IPackageStateStore _stateStore = Substitute.For<IPackageStateStore>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IPaths _paths = Substitute.For<IPaths>();
    private readonly PackageService _service;
    private readonly string _nativeToolsDirectory =
        Path.Combine(Path.GetTempPath(), $"test-nativetools-{Guid.NewGuid()}");

    public PackageServiceTests()
    {
        _paths.NativeToolsDirectory.Returns(_nativeToolsDirectory);
        _paths.PackagesDirectory.Returns(_nativeToolsDirectory);
        _paths.SettingsPath.Returns(Path.Combine(_nativeToolsDirectory, "settings.json"));
        _settingsService.GetSettingValue<System.Collections.ObjectModel.ObservableCollection<string>>(
            "PackageManager_Sources").Returns([]);
        _settingsService.GetSettingValue<bool>("PackageManager_OnlyCustomSources").Returns(false);
        _catalog.RefreshAsync(Arg.Any<IEnumerable<string[]>>(), Arg.Any<CancellationToken>()).Returns(true);

        _service = new PackageService(_catalog, _downloader, _stateStore, _settingsService,
            Substitute.For<ILogger>(), Substitute.For<IApplicationStateService>(),
            Substitute.For<IHttpService>(), _paths, Substitute.For<ICompositeServiceProvider>(),
            new GenericPackageInstaller());
    }

    /// <summary>
    ///     A package installed from a prerelease version must not look uninstalled just because its
    ///     version string is not a <see cref="Version" />, otherwise it can never be removed again.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_KeepsPrereleaseOnlyPackageRemovable()
    {
        SetInstalled(new InstalledPackage("tsgo", "NativeTool", "tsgo", "Binaries", null, null,
            "7.0.0-dev.20260707.2"));
        _catalog.Manifests.Returns(new Dictionary<string, Package>());

        await _service.RefreshAsync(true);

        var state = _service.Packages["tsgo"];
        Assert.Equal(PackageStatus.Installed, state.Status);
        Assert.Equal("7.0.0-dev.20260707.2", state.InstalledVersion?.Version);
    }

    [Fact]
    public async Task RemoveAsync_DropsPackageThatNoRepositoryOffersAnymore()
    {
        SetInstalled(new InstalledPackage("tsgo", "NativeTool", "tsgo", "Binaries", null, null,
            "7.0.0-dev.20260707.2"));
        _catalog.Manifests.Returns(new Dictionary<string, Package>());

        await _service.RefreshAsync(true);

        Assert.True(await _service.RemoveAsync("tsgo"));
        Assert.False(_service.Packages.ContainsKey("tsgo"));
    }

    [Fact]
    public async Task RemoveAsync_KeepsPackageThatIsStillOffered()
    {
        var package = new Package
        {
            Id = "still-there",
            Type = "NativeTool",
            Name = "Still There",
            Versions = [new PackageVersion { Version = "1.0.0" }]
        };

        SetInstalled(new InstalledPackage("still-there", "NativeTool", "Still There", null, null, null, "1.0.0"));
        _catalog.Manifests.Returns(new Dictionary<string, Package> { ["still-there"] = package });

        await _service.RefreshAsync(true);

        Assert.True(await _service.RemoveAsync("still-there"));
        Assert.True(_service.Packages.ContainsKey("still-there"));
        Assert.Null(_service.Packages["still-there"].InstalledVersion);
    }

    private void SetInstalled(params InstalledPackage[] packages)
    {
        _stateStore.LoadAsync().Returns(packages.ToDictionary(x => x.Id));
    }
}
