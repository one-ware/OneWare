using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Services;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class ConfigurationProfileServiceTests
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IPackageService _packageService = Substitute.For<IPackageService>();
    private readonly IHttpService _httpService = Substitute.For<IHttpService>();
    private readonly IPaths _paths = Substitute.For<IPaths>();
    private readonly ILogger<ConfigurationProfileService> _logger = Substitute.For<ILogger<ConfigurationProfileService>>();
    private readonly ConfigurationProfileService _service;

    public ConfigurationProfileServiceTests()
    {
        _paths.SettingsPath.Returns(Path.Combine(Path.GetTempPath(), $"test-settings-{Guid.NewGuid()}.json"));
        _paths.AppDataDirectory.Returns(Path.Combine(Path.GetTempPath(), $"test-appdata-{Guid.NewGuid()}"));
        _service = new ConfigurationProfileService(_settingsService, _packageService, _httpService, _paths, _logger);
    }

    [Fact]
    public async Task ExportAsync_CapturesInstalledPackages()
    {
        var packageState = Substitute.For<IPackageState>();
        packageState.InstalledVersion.Returns(new PackageVersion { Version = "1.0.0" });

        _packageService.Packages.Returns(new Dictionary<string, IPackageState>
        {
            ["test-plugin"] = packageState
        });

        _settingsService.HasSetting("PackageManager_Sources").Returns(false);

        var profile = await _service.ExportAsync();

        Assert.Single(profile.Packages);
        Assert.Equal("test-plugin", profile.Packages[0].Id);
        Assert.Equal("1.0.0", profile.Packages[0].Version);
    }

    [Fact]
    public async Task ExportAsync_SkipsUninstalledPackages()
    {
        var packageState = Substitute.For<IPackageState>();
        packageState.InstalledVersion.Returns((PackageVersion?)null);

        _packageService.Packages.Returns(new Dictionary<string, IPackageState>
        {
            ["not-installed"] = packageState
        });

        _settingsService.HasSetting("PackageManager_Sources").Returns(false);

        var profile = await _service.ExportAsync();

        Assert.Empty(profile.Packages);
    }

    [Fact]
    public async Task SaveToFileAsync_And_LoadFromFileAsync_RoundTrip()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-profile-{Guid.NewGuid()}.onewareconfig");
        try
        {
            var profile = new ConfigurationProfile
            {
                Name = "Test Profile",
                Description = "A test configuration",
                ExportedAt = DateTimeOffset.UtcNow,
                Settings = new Dictionary<string, object?>
                {
                    ["Editor_FontSize"] = JsonSerializer.SerializeToElement(14),
                    ["General_SelectedTheme"] = JsonSerializer.SerializeToElement("Dark")
                },
                Packages =
                [
                    new ConfigurationProfilePackage { Id = "ghdl-plugin", Version = "2.0.0" }
                ],
                PackageSources = ["https://example.com/repo"]
            };

            await _service.SaveToFileAsync(profile, tempPath);
            var loaded = await _service.LoadFromFileAsync(tempPath);

            Assert.Equal(profile.Name, loaded.Name);
            Assert.Equal(profile.Description, loaded.Description);
            Assert.Equal(profile.Version, loaded.Version);
            Assert.Single(loaded.Packages);
            Assert.Equal("ghdl-plugin", loaded.Packages[0].Id);
            Assert.Equal("2.0.0", loaded.Packages[0].Version);
            Assert.Single(loaded.PackageSources);
            Assert.Equal("https://example.com/repo", loaded.PackageSources[0]);
            Assert.Equal(2, loaded.Settings.Count);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ImportAsync_InstallsPackages()
    {
        var profile = new ConfigurationProfile
        {
            Packages =
            [
                new ConfigurationProfilePackage { Id = "test-plugin", Version = "1.0.0" }
            ]
        };

        var resolvedVersion = new PackageVersion { Version = "1.0.0" };
        var packageState = Substitute.For<IPackageState>();
        packageState.Package.Returns(new Package
        {
            Id = "test-plugin",
            Versions =
            [
                resolvedVersion
            ]
        });
        packageState.InstalledVersion.Returns((PackageVersion?)null);

        _packageService.Packages.Returns(new Dictionary<string, IPackageState>
        {
            ["test-plugin"] = packageState
        });
        _packageService.RefreshAsync().Returns(true);
        _packageService.InstallAsync(
                Arg.Any<string>(),
                Arg.Any<PackageVersion?>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new PackageInstallResult { Status = PackageInstallResultReason.Installed });

        await _service.ImportAsync(profile);

        await _packageService.Received(1).InstallAsync(
            "test-plugin",
            Arg.Is<PackageVersion?>(v => v != null && v.Version == "1.0.0"),
            false,
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_SkipsAlreadyInstalledPackages()
    {
        var profile = new ConfigurationProfile
        {
            Packages =
            [
                new ConfigurationProfilePackage { Id = "already-installed", Version = "1.0.0" }
            ]
        };

        var packageState = Substitute.For<IPackageState>();
        packageState.InstalledVersion.Returns(new PackageVersion { Version = "1.0.0" });

        _packageService.Packages.Returns(new Dictionary<string, IPackageState>
        {
            ["already-installed"] = packageState
        });
        _packageService.RefreshAsync().Returns(true);

        await _service.ImportAsync(profile);

        await _packageService.DidNotReceive().InstallAsync(
            Arg.Any<string>(),
            Arg.Any<PackageVersion?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_AppliesSettings()
    {
        var settingValue = JsonSerializer.SerializeToElement("Dark");
        var profile = new ConfigurationProfile
        {
            Settings = new Dictionary<string, object?>
            {
                ["General_SelectedTheme"] = settingValue
            }
        };

        _settingsService.HasSetting("General_SelectedTheme").Returns(true);
        _settingsService.GetSetting("General_SelectedTheme").Returns(new Setting("Light"));

        _packageService.Packages.Returns(new Dictionary<string, IPackageState>());

        await _service.ImportAsync(profile);

        _settingsService.Received(1).SetSettingValue("General_SelectedTheme", "Dark");
        _settingsService.Received().Save(Arg.Any<string>(), false);
    }

    [Fact]
    public async Task ExportAsync_SetsExportTimestamp()
    {
        _packageService.Packages.Returns(new Dictionary<string, IPackageState>());
        _settingsService.HasSetting("PackageManager_Sources").Returns(false);

        var before = DateTimeOffset.UtcNow;
        var profile = await _service.ExportAsync();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(profile.ExportedAt, before, after);
    }

    [Fact]
    public async Task ExportAsync_VersionIs1()
    {
        _packageService.Packages.Returns(new Dictionary<string, IPackageState>());
        _settingsService.HasSetting("PackageManager_Sources").Returns(false);

        var profile = await _service.ExportAsync();

        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public async Task LoadFromSourceAsync_ReadsLocalFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-profile-{Guid.NewGuid()}.onewareconfig");
        try
        {
            await _service.SaveToFileAsync(new ConfigurationProfile { Name = "From File" }, tempPath);

            var loaded = await _service.LoadFromSourceAsync(tempPath);

            Assert.Equal("From File", loaded.Name);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task LoadFromSourceAsync_DownloadsHttpUrl()
    {
        const string url = "https://config.example.com/team.onewareconfig";
        _httpService.DownloadTextAsync(url, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("""{"name":"From Url"}""");

        var loaded = await _service.LoadFromSourceAsync(url);

        Assert.Equal("From Url", loaded.Name);
    }

    [Fact]
    public async Task LoadFromSourceAsync_ThrowsWhenDownloadFails()
    {
        const string url = "https://config.example.com/missing.onewareconfig";
        _httpService.DownloadTextAsync(url, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LoadFromSourceAsync(url));
    }

    [Fact]
    public async Task ApplyEnvironmentProfileAsync_ReturnsFalseWhenVariableNotSet()
    {
        using var _ = new EnvironmentVariableScope(IConfigurationProfileService.ProfileEnvironmentVariable, null);

        Assert.False(await _service.ApplyEnvironmentProfileAsync());
    }

    [Fact]
    public async Task ApplyEnvironmentProfileAsync_AppliesProfileOnceThenSkips()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-profile-{Guid.NewGuid()}.onewareconfig");
        try
        {
            await _service.SaveToFileAsync(
                new ConfigurationProfile { Settings = { ["General_SelectedTheme"] = JsonSerializer.SerializeToElement("Dark") } },
                tempPath);

            _settingsService.HasSetting("General_SelectedTheme").Returns(true);
            _settingsService.GetSetting("General_SelectedTheme").Returns(new Setting("Light"));
            _packageService.Packages.Returns(new Dictionary<string, IPackageState>());

            using var _ = new EnvironmentVariableScope(IConfigurationProfileService.ProfileEnvironmentVariable, tempPath);
            using var __ = new EnvironmentVariableScope(IConfigurationProfileService.ProfileModeEnvironmentVariable, null);

            Assert.True(await _service.ApplyEnvironmentProfileAsync());

            // Unchanged profile must not be re-applied, so user edits survive the next launch.
            Assert.False(await _service.ApplyEnvironmentProfileAsync());

            _settingsService.Received(1).SetSettingValue("General_SelectedTheme", "Dark");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (Directory.Exists(_paths.AppDataDirectory)) Directory.Delete(_paths.AppDataDirectory, true);
        }
    }

    [Fact]
    public async Task ApplyEnvironmentProfileAsync_ReAppliesInAlwaysMode()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-profile-{Guid.NewGuid()}.onewareconfig");
        try
        {
            await _service.SaveToFileAsync(
                new ConfigurationProfile { Settings = { ["General_SelectedTheme"] = JsonSerializer.SerializeToElement("Dark") } },
                tempPath);

            _settingsService.HasSetting("General_SelectedTheme").Returns(true);
            _settingsService.GetSetting("General_SelectedTheme").Returns(new Setting("Light"));
            _packageService.Packages.Returns(new Dictionary<string, IPackageState>());

            using var _ = new EnvironmentVariableScope(IConfigurationProfileService.ProfileEnvironmentVariable, tempPath);
            using var __ = new EnvironmentVariableScope(IConfigurationProfileService.ProfileModeEnvironmentVariable, "always");

            Assert.True(await _service.ApplyEnvironmentProfileAsync());
            Assert.True(await _service.ApplyEnvironmentProfileAsync());

            _settingsService.Received(2).SetSettingValue("General_SelectedTheme", "Dark");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (Directory.Exists(_paths.AppDataDirectory)) Directory.Delete(_paths.AppDataDirectory, true);
        }
    }

    [Fact]
    public async Task ApplyEnvironmentProfileAsync_ReturnsFalseWhenProfileIsMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.onewareconfig");
        using var _ = new EnvironmentVariableScope(IConfigurationProfileService.ProfileEnvironmentVariable, missingPath);

        // A broken profile must be logged and skipped rather than block startup.
        Assert.False(await _service.ApplyEnvironmentProfileAsync());
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _original;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
    }
}
