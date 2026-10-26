using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Installers;
using OneWare.PackageManager.Services;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class PackageDependencyTests : IDisposable
{
    private readonly IPackageCatalog _catalog = Substitute.For<IPackageCatalog>();
    private readonly IPackageDownloader _downloader = Substitute.For<IPackageDownloader>();
    private readonly IPackageStateStore _stateStore = Substitute.For<IPackageStateStore>();
    private readonly IPackageInstaller _installer = Substitute.For<IPackageInstaller>();
    private readonly IPaths _paths = Substitute.For<IPaths>();
    private readonly ICompositeServiceProvider _serviceProvider = Substitute.For<ICompositeServiceProvider>();
    private readonly List<string> _installed = [];
    private readonly PackageService _service;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"test-dependencies-{Guid.NewGuid()}");

    public PackageDependencyTests()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingValue<System.Collections.ObjectModel.ObservableCollection<string>>(
            "PackageManager_Sources").Returns([]);
        _catalog.RefreshAsync(Arg.Any<IEnumerable<string[]>>(), Arg.Any<CancellationToken>()).Returns(true);

        var target = new PackageTarget { Target = "all", Url = "https://example.invalid/plugin.zip" };
        _serviceProvider.GetService(typeof(IPackageInstaller)).Returns(_installer);
        _installer.GetExtractionPath(Arg.Any<Package>(), _paths).Returns(_directory);
        _installer.SelectTarget(Arg.Any<Package>(), Arg.Any<PackageVersion>()).Returns(target);
        _installer.CheckCompatibilityAsync(Arg.Any<Package>(), Arg.Any<PackageVersion>(), Arg.Any<CancellationToken>())
            .Returns(new CompatibilityReport(true));
        _installer.RemoveAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>())
            .Returns(new PackageInstallerResult(PackageStatus.Available));
        _installer.InstallAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                var context = x.Arg<PackageInstallContext>();
                _installed.Add($"{context.Package.Id} {context.Version.Version}");
                return new PackageInstallerResult(PackageStatus.Installed);
            });
        _downloader.DownloadAndExtractAsync(Arg.Any<string>(), _directory, Arg.Any<bool>(),
                Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Directory.CreateDirectory(_directory);
                return true;
            });

        _service = new PackageService(_catalog, _downloader, _stateStore, settingsService,
            Substitute.For<ILogger>(), Substitute.For<IApplicationStateService>(),
            Substitute.For<IHttpService>(), _paths, _serviceProvider, new GenericPackageInstaller());
        _service.RegisterInstaller<IPackageInstaller>("Plugin");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public async Task InstallAsync_InstallsMissingDependencyFirst()
    {
        await SetupAsync([Base("1.0.0", "1.2.0", "2.0.0-beta.1"), Extension("1.2.0")]);

        var result = await _service.InstallAsync("extension");

        Assert.Equal(PackageInstallResultReason.Installed, result.Status);
        Assert.Equal(["base 1.2.0", "extension 1.0.0"], _installed);
        Assert.Equal(PackageStatus.Installed, _service.Packages["base"].Status);
    }

    [Fact]
    public async Task InstallAsync_KeepsInstalledDependencyThatSatisfiesMinVersion()
    {
        await SetupAsync([Base("1.0.0", "1.2.0", "1.3.0"), Extension("1.2.0")],
            new InstalledPackage("base", "Plugin", "Base", null, null, null, "1.2.0"));

        await _service.InstallAsync("extension");

        Assert.Equal(["extension 1.0.0"], _installed);
        Assert.Equal("1.2.0", _service.Packages["base"].InstalledVersion?.Version);
    }

    [Fact]
    public async Task InstallAsync_UpdatesOutdatedDependency()
    {
        await SetupAsync([Base("1.0.0", "1.2.0"), Extension("1.2.0")],
            new InstalledPackage("base", "Plugin", "Base", null, null, null, "1.0.0"));

        await _service.InstallAsync("extension");

        Assert.Equal(["base 1.2.0", "extension 1.0.0"], _installed);
    }

    [Fact]
    public async Task InstallAsync_UsesPrereleaseOnlyWhenNoStableVersionSatisfiesDependency()
    {
        await SetupAsync([Base("1.0.0", "2.0.0-beta.1"), Extension("2.0.0-beta.1")]);

        var plan = _service.ResolveDependencies("extension", _service.Packages["extension"].Package.Versions![0]);

        Assert.Equal("2.0.0-beta.1", Assert.Single(plan.ToInstall).Version.Version);
    }

    [Fact]
    public async Task InstallAsync_FailsWhenDependencyIsNotAvailable()
    {
        await SetupAsync([Base("1.0.0"), Extension("2.0.0")]);

        var result = await _service.InstallAsync("extension");

        Assert.Equal(PackageInstallResultReason.MissingDependency, result.Status);
        Assert.Empty(_installed);
        Assert.Null(_service.Packages["extension"].InstalledVersion);
    }

    [Fact]
    public async Task InstallAsync_HandlesDependencyCycles()
    {
        await SetupAsync([
            new Package
            {
                Id = "a", Type = "Plugin", Name = "A",
                Versions = [new PackageVersion { Version = "1.0.0", Dependencies = [new PackageDependency { Id = "b" }] }]
            },
            new Package
            {
                Id = "b", Type = "Plugin", Name = "B",
                Versions = [new PackageVersion { Version = "1.0.0", Dependencies = [new PackageDependency { Id = "a" }] }]
            }
        ]);

        var result = await _service.InstallAsync("a");

        Assert.Equal(PackageInstallResultReason.Installed, result.Status);
        Assert.Equal(["b 1.0.0", "a 1.0.0"], _installed);
    }

    [Fact]
    public async Task GetInstalledDependents_ReturnsInstalledPackagesThatRequireThePackage()
    {
        await SetupAsync([Base("1.2.0"), Extension("1.2.0")],
            new InstalledPackage("base", "Plugin", "Base", null, null, null, "1.2.0"),
            new InstalledPackage("extension", "Plugin", "Extension", null, null, null, "1.0.0"));

        var dependents = _service.GetInstalledDependents("base");

        Assert.Equal("extension", Assert.Single(dependents).Package.Id);
        Assert.Empty(_service.GetInstalledDependents("extension"));
    }

    private async Task SetupAsync(Package[] packages, params InstalledPackage[] installed)
    {
        _catalog.Manifests.Returns(packages.ToDictionary(x => x.Id!));
        _stateStore.LoadAsync().Returns(installed.ToDictionary(x => x.Id));
        await _service.RefreshAsync(true);
    }

    private static Package Base(params string[] versions)
    {
        return new Package
        {
            Id = "base",
            Type = "Plugin",
            Name = "Base",
            Versions = versions.Select(x => new PackageVersion
            {
                Version = x,
                IsPrerelease = x.Contains('-')
            }).ToArray()
        };
    }

    private static Package Extension(string minBaseVersion)
    {
        return new Package
        {
            Id = "extension",
            Type = "Plugin",
            Name = "Extension",
            Versions =
            [
                new PackageVersion
                {
                    Version = "1.0.0",
                    Dependencies = [new PackageDependency { Id = "base", MinVersion = minBaseVersion }]
                }
            ]
        };
    }
}
