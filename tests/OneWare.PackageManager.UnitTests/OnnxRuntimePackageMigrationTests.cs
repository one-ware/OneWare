using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Services;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class OnnxRuntimePackageMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"onnx-migration-{Guid.NewGuid()}");
    private readonly IPackageStateStore _stateStore = Substitute.For<IPackageStateStore>();
    private readonly IPaths _paths = Substitute.For<IPaths>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILogger<OnnxRuntimePackageMigration> _logger =
        Substitute.For<ILogger<OnnxRuntimePackageMigration>>();

    public OnnxRuntimePackageMigrationTests()
    {
        _paths.OnnxRuntimesDirectory.Returns(Path.Combine(_root, "runtimes"));
        _paths.SettingsPath.Returns(Path.Combine(_root, "settings.json"));
    }

    [Fact]
    public async Task MigrateAsync_RemovesOutdatedAndRetiredRuntimes()
    {
        var oldNvidia = RuntimePackage("onnxruntime-nvidia", "1.23.2");
        var retiredQnn = RuntimePackage("onnxruntime-qnn", "1.23.2");
        var plugin = new InstalledPackage("plugin", "Plugin", "Plugin", null, null, null, "1.0.0");
        var installed = new Dictionary<string, InstalledPackage>
        {
            [oldNvidia.Id] = oldNvidia,
            [retiredQnn.Id] = retiredQnn,
            [plugin.Id] = plugin
        };

        _stateStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(installed);
        _settingsService.GetSettingValue<string>("OnnxRuntime_SelectedRuntime").Returns(retiredQnn.Id);
        CreateRuntimeDirectory(oldNvidia.Id);
        CreateRuntimeDirectory(retiredQnn.Id);

        await CreateMigration().MigrateAsync();

        Assert.False(Directory.Exists(Path.Combine(_paths.OnnxRuntimesDirectory, oldNvidia.Id)));
        Assert.False(Directory.Exists(Path.Combine(_paths.OnnxRuntimesDirectory, retiredQnn.Id)));
        await _stateStore.Received(1).SaveAsync(
            Arg.Is<IEnumerable<InstalledPackage>>(packages =>
                packages.Count() == 1 && packages.Single().Id == plugin.Id),
            Arg.Any<CancellationToken>());
        _settingsService.Received(1).SetSettingValue("OnnxRuntime_SelectedRuntime", "onnxruntime-builtin");
        _settingsService.Received(1)
            .SetSettingValue("OnnxRuntime_SelectedExecutionProvider", OnnxExecutionProvider.Cpu);
        _settingsService.Received(1).Save(_paths.SettingsPath);
    }

    [Fact]
    public async Task MigrateAsync_PreservesCurrentRuntime()
    {
        var current = RuntimePackage("onnxruntime-nvidia", "1.28.0");
        _stateStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, InstalledPackage> { [current.Id] = current });
        CreateRuntimeDirectory(current.Id);

        await CreateMigration().MigrateAsync();

        Assert.True(Directory.Exists(Path.Combine(_paths.OnnxRuntimesDirectory, current.Id)));
        await _stateStore.DidNotReceive()
            .SaveAsync(Arg.Any<IEnumerable<InstalledPackage>>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private OnnxRuntimePackageMigration CreateMigration()
    {
        return new OnnxRuntimePackageMigration(_stateStore, _paths, _settingsService, _logger);
    }

    private void CreateRuntimeDirectory(string packageId)
    {
        Directory.CreateDirectory(Path.Combine(_paths.OnnxRuntimesDirectory, packageId));
    }

    private static InstalledPackage RuntimePackage(string id, string version)
    {
        return new InstalledPackage(id, "OnnxRuntime", id, "ONNX Runtimes", null, "MIT", version);
    }
}
