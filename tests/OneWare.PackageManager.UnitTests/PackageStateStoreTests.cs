using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Services;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public sealed class PackageStateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "oneware-state-" + Guid.NewGuid().ToString("N"));
    private readonly PackageStateStore _store;
    public PackageStateStoreTests()
    {
        var paths = Substitute.For<IPaths>();
        paths.PackagesDirectory.Returns(_directory);
        paths.AppName.Returns("Test");
        _store = new(paths, Substitute.For<ILogger>());
    }

    [Fact]
    public async Task RoundTripsGraphAndLegacyRecordsAndReplacesLongerFile()
    {
        await _store.SaveAsync([new InstalledPackage("A", "Plugin", "A", null, null, null, "1")
        { Dependencies = [new PackageDependency { Id = "B", MinVersion = "1", MaxVersionExclusive = "2" }],
            ResolvedDependencies = new() { ["B"] = "1.2" }, Source = "official" }]);
        var record = (await _store.LoadAsync())["A"];
        Assert.Equal("1.2", record.ResolvedDependencies!["B"]);
        Assert.Equal("2", record.Dependencies![0].MaxVersionExclusive);
        await _store.SaveAsync([new InstalledPackage("Old", "Plugin", "Old", null, null, null, "1")]);
        Assert.Null((await _store.LoadAsync())["Old"].Dependencies);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task CancelledWritePreservesPreviousDatabase()
    {
        await _store.SaveAsync([new InstalledPackage("A", "Plugin", "A", null, null, null, "1")]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SaveAsync([], new CancellationToken(true)));
        Assert.Contains("A", (await _store.LoadAsync()).Keys);
    }

    [Fact]
    public async Task CorruptStateFailsClosedInsteadOfPretendingNothingInstalled()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "test-packages.json"), "not json");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _store.LoadAsync());
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}