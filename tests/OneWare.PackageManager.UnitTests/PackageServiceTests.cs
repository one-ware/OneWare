using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
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
    private readonly ICompositeServiceProvider _serviceProvider = Substitute.For<ICompositeServiceProvider>();
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
            Substitute.For<IHttpService>(), _paths, _serviceProvider,
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

        var updates = 0;
        _service.PackagesUpdated += (_, _) => updates++;

        Assert.True(await _service.RemoveAsync("tsgo"));
        Assert.False(_service.Packages.ContainsKey("tsgo"));
        Assert.Equal(1, updates);
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

        var updates = 0;
        _service.PackagesUpdated += (_, _) => updates++;

        Assert.True(await _service.RemoveAsync("still-there"));
        Assert.True(_service.Packages.ContainsKey("still-there"));
        Assert.Null(_service.Packages["still-there"].InstalledVersion);
        Assert.Equal(0, updates);
    }

    [Theory]
    [InlineData(false, PackageStatus.NeedRestart, "2.0.0", PackageStatus.NeedRestart)]
    [InlineData(true, PackageStatus.NeedRestart, "2.0.0", PackageStatus.NeedRestart)]
    [InlineData(false, PackageStatus.Installed, "2.0.0", PackageStatus.Installed)]
    [InlineData(true, PackageStatus.Installed, "2.0.0", PackageStatus.Installed)]
    [InlineData(false, PackageStatus.Installed, "1.5.0", PackageStatus.UpdateAvailable)]
    [InlineData(true, PackageStatus.Installed, "1.5.0", PackageStatus.UpdateAvailable)]
    public async Task InstallOrUpdateAsync_PreservesInstallerStatusWithoutRebuildingPackages(
        bool update, PackageStatus installerStatus, string version, PackageStatus expectedStatus)
    {
        var target = new PackageTarget { Target = "all", Url = "https://example.invalid/plugin.zip" };
        var package = new Package
        {
            Id = "plugin",
            Type = "Plugin",
            Name = "Plugin",
            Versions =
            [
                new PackageVersion { Version = "1.0.0" },
                new PackageVersion { Version = "1.5.0", Targets = [target] },
                new PackageVersion { Version = "2.0.0", Targets = [target] }
            ]
        };
        var installer = Substitute.For<IPackageInstaller>();
        _serviceProvider.GetService(typeof(IPackageInstaller)).Returns(installer);
        _service.RegisterInstaller<IPackageInstaller>("Plugin");
        installer.GetExtractionPath(package, _paths).Returns(_nativeToolsDirectory);
        installer.SelectTarget(package, Arg.Any<PackageVersion>()).Returns(target);
        installer.CheckCompatibilityAsync(package, Arg.Any<PackageVersion>(), Arg.Any<CancellationToken>())
            .Returns(new CompatibilityReport(true));
        installer.RemoveAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>())
            .Returns(new PackageInstallerResult(installerStatus));
        installer.InstallAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>())
            .Returns(new PackageInstallerResult(installerStatus));
        _downloader.DownloadAndExtractAsync(Arg.Any<string>(), _nativeToolsDirectory, Arg.Any<bool>(),
                Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Directory.CreateDirectory(_nativeToolsDirectory);
                return true;
            });
        _catalog.Manifests.Returns(new Dictionary<string, Package> { ["plugin"] = package });
        SetInstalled(update
            ? [new InstalledPackage("plugin", "Plugin", "Plugin", null, null, null, "1.0.0")]
            : []);
        await _service.RefreshAsync(true);
        var state = _service.Packages["plugin"];
        var updates = 0;
        _service.PackagesUpdated += (_, _) => updates++;

        try
        {
            var selectedVersion = package.Versions.Single(x => x.Version == version);
            var result = update
                ? await _service.UpdateAsync("plugin", selectedVersion)
                : await _service.InstallAsync("plugin", selectedVersion);

            Assert.Equal(PackageInstallResultReason.Installed, result.Status);
            Assert.Equal(expectedStatus, state.Status);
            Assert.Equal(version, state.InstalledVersion?.Version);
            Assert.Same(state, _service.Packages["plugin"]);
            Assert.Equal(0, updates);
        }
        finally
        {
            if (Directory.Exists(_nativeToolsDirectory))
                Directory.Delete(_nativeToolsDirectory, true);
        }
    }

    private void SetInstalled(params InstalledPackage[] packages)
    {
        _stateStore.LoadAsync().Returns(packages.ToDictionary(x => x.Id));
    }
}
