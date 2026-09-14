using System.Collections.ObjectModel;
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

public sealed class PackageOperationTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "oneware-graph-" + Guid.NewGuid().ToString("N"));
    private readonly IPackageCatalog _catalog = Substitute.For<IPackageCatalog>();
    private readonly IPackageStateStore _store = Substitute.For<IPackageStateStore>();
    private readonly IPackageDownloader _downloader = Substitute.For<IPackageDownloader>();
    private readonly IPackageInstaller _installer = Substitute.For<IPackageInstaller>();
    private readonly PackageService _service;
    private readonly List<string> _activated = [];
    private InstalledPackage[] _saved = [];

    public PackageOperationTests()
    {
        var paths = Substitute.For<IPaths>();
        paths.PluginsDirectory.Returns(_folder);
        paths.PackagesDirectory.Returns(_folder);
        var settings = Substitute.For<ISettingsService>();
        settings.GetSettingValue<ObservableCollection<string>>("PackageManager_Sources").Returns([]);
        var provider = Substitute.For<ICompositeServiceProvider>();
        provider.GetService(typeof(IPackageInstaller)).Returns(_installer);
        _installer.GetExtractionPath(Arg.Any<Package>(), paths).Returns(x => Path.Combine(_folder, x.Arg<Package>().Id!));
        _installer.SelectTarget(Arg.Any<Package>(), Arg.Any<PackageVersion>()).Returns(x => x.Arg<PackageVersion>().Targets?.FirstOrDefault());
        _installer.CheckCompatibilityAsync(Arg.Any<Package>(), Arg.Any<PackageVersion>(), Arg.Any<CancellationToken>()).Returns(new CompatibilityReport(true));
        _installer.InstallAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            _activated.Add(x.Arg<PackageInstallContext>().Package.Id!);
            return new PackageInstallerResult(PackageStatus.Installed);
        });
        _installer.RemoveAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>()).Returns(new PackageInstallerResult(PackageStatus.Available));
        _downloader.DownloadAndExtractAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            var path = x.ArgAt<string>(1);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "payload"), x.ArgAt<string>(0));
            return true;
        });
        _store.SaveAsync(Arg.Any<IEnumerable<InstalledPackage>>(), Arg.Any<CancellationToken>()).Returns(x =>
        { _saved = x.Arg<IEnumerable<InstalledPackage>>().ToArray(); return Task.CompletedTask; });
        _store.LoadAsync().Returns(_ => _saved.ToDictionary(x => x.Id));
        _catalog.RefreshAsync(Arg.Any<IEnumerable<string[]>>(), Arg.Any<CancellationToken>()).Returns(true);
        _service = new PackageService(_catalog, _downloader, _store, settings, Substitute.For<ILogger>(),
            Substitute.For<IApplicationStateService>(), Substitute.For<IHttpService>(), paths, provider, new GenericPackageInstaller());
        _service.RegisterInstaller<IPackageInstaller>("Plugin");
    }

    private static Package P(string id, bool license = false, params string[] dependencies) => new()
    {
        Id = id, Name = id, Type = "Plugin", AcceptLicenseBeforeDownload = license,
        Versions = [new PackageVersion { Version = "1", Dependencies = dependencies.Select(x => new PackageDependency { Id = x }).ToArray(),
            Targets = [new PackageTarget { Target = "all", Url = "https://example.invalid/" + id }] }]
    };
    private async Task Load(params Package[] packages)
    {
        _catalog.Manifests.Returns(packages.ToDictionary(x => x.Id!));
        await _service.RefreshAsync(true);
    }

    [Fact]
    public async Task DependencyConsentCannotBeBypassedByLegacyEntrypoint()
    {
        await Load(P("A", false, "B"), P("B", true));
        Assert.Equal(PackageInstallResultReason.ConsentRequired, (await _service.InstallAsync("A")).Status);
        Assert.Empty(_activated);
        Assert.False(Directory.Exists(_folder));
        var plan = await _service.PlanAsync([new("A")]);
        var result = await _service.ExecuteAsync(plan, ["B"]);
        Assert.Equal(PackageInstallResultReason.Installed, result.Status);
        Assert.Equal(["B", "A"], _activated);
        Assert.Equal("1", _saved.Single(x => x.Id == "A").ResolvedDependencies!["B"]);
    }

    [Fact]
    public async Task PersistedGraphBlocksOfflineRemovalAndRetainsUnusedDependencies()
    {
        await Load(P("A", false, "B"), P("B"));
        Assert.Equal(PackageInstallResultReason.Installed, (await _service.InstallAsync("A")).Status);
        await Load();
        Assert.False(await _service.RemoveAsync("B"));
        Assert.True(await _service.RemoveAsync("A"));
        Assert.NotNull(_service.Packages["B"].InstalledVersion);
        Assert.True(await _service.RemoveAsync("B"));
    }

    [Fact]
    public async Task AllDownloadsFinishBeforeAnyActivationAndFailureLeavesOldFiles()
    {
        await Load(P("A", false, "B"), P("B"));
        _downloader.DownloadAndExtractAsync("https://example.invalid/A", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>()).Returns(false);
        Assert.Equal(PackageInstallResultReason.ErrorDownloading, (await _service.InstallAsync("A")).Status);
        Assert.Empty(_activated);
        Assert.Empty(_saved);
        Assert.Empty(Directory.GetDirectories(_folder));
    }

    [Fact]
    public async Task RejectedCompatibilityPreservesExistingInstallation()
    {
        await Load(P("A"));
        await _service.InstallAsync("A");
        var original = File.ReadAllText(Path.Combine(_folder, "A", "payload"));
        var next = P("A");
        next.Versions = [new PackageVersion { Version = "2", Targets = next.Versions![0].Targets }];
        await Load(next);
        _installer.CheckCompatibilityAsync(Arg.Any<Package>(), Arg.Any<PackageVersion>(), Arg.Any<CancellationToken>()).Returns(new CompatibilityReport(false));
        Assert.Equal(PackageInstallResultReason.Incompatible, (await _service.UpdateAsync("A")).Status);
        Assert.Equal("1", _service.Packages["A"].InstalledVersion?.Version);
        Assert.Equal(original, File.ReadAllText(Path.Combine(_folder, "A", "payload")));
    }

    [Fact]
    public async Task StateWriteFailureRestoresPreviousDirectory()
    {
        await Load(P("A"));
        await _service.InstallAsync("A");
        var next = P("A");
        next.Versions = [new PackageVersion { Version = "2", Targets = [new PackageTarget { Target = "all", Url = "https://example.invalid/new" }] }];
        await Load(next);
        _store.SaveAsync(Arg.Any<IEnumerable<InstalledPackage>>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            if (x.Arg<IEnumerable<InstalledPackage>>().Any(p => p.InstalledVersion == "2")) throw new IOException("write failed");
            return Task.CompletedTask;
        });
        Assert.Equal(PackageInstallResultReason.ErrorDownloading, (await _service.UpdateAsync("A")).Status);
        Assert.Equal("1", _service.Packages["A"].InstalledVersion?.Version);
        Assert.Equal("https://example.invalid/A", File.ReadAllText(Path.Combine(_folder, "A", "payload")));
    }

    [Fact]
    public async Task FailedParentReportsCompletedDependency()
    {
        await Load(P("A", false, "B"), P("B"));
        _installer.InstallAsync(Arg.Is<PackageInstallContext>(x => x.Package.Id == "A"), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PackageInstallerResult>(new IOException("activation failed")));
        var result = await _service.InstallAsync("A");
        Assert.Equal(PackageInstallResultReason.ErrorDownloading, result.Status);
        Assert.Equal(["B"], result.CompletedPackages);
        Assert.Null(_service.Packages["A"].InstalledVersion);
        Assert.NotNull(_service.Packages["B"].InstalledVersion);
    }

    [Fact]
    public async Task ChangedPreviewRequiresReviewAgain()
    {
        await Load(P("A"));
        var plan = await _service.PlanAsync([new("A")]);
        await Load(P("A"), P("B"));
        Assert.Equal(PackageInstallResultReason.PlanChanged, (await _service.ExecuteAsync(plan, [])).Status);
        Assert.Empty(_activated);
    }

    [Fact]
    public async Task ChangedSelectedTargetRequiresReviewEvenIfVersionIsUnchanged()
    {
        await Load(P("A"));
        var plan = await _service.PlanAsync([new("A")]);
        _installer.SelectTarget(Arg.Any<Package>(), Arg.Any<PackageVersion>())
            .Returns(new PackageTarget { Target = "all", Url = "https://example.invalid/different" });
        Assert.Equal(PackageInstallResultReason.PlanChanged, (await _service.ExecuteAsync(plan, [])).Status);
        Assert.Empty(_activated);
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public async Task FailedActivationRollsBackPersistenceAndRetryDoesNotHotLoadAgain()
    {
        await Load(P("A"));
        _installer.InstallAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PackageInstallerResult>(new IOException("module initialization failed")));
        var failed = await _service.InstallAsync("A");
        Assert.Equal(PackageInstallResultReason.ErrorDownloading, failed.Status);
        Assert.True(failed.RestartRequired);
        Assert.Empty(_saved);
        Assert.False(Directory.Exists(Path.Combine(_folder, "A")));
        var retry = await _service.InstallAsync("A");
        Assert.Equal(PackageInstallResultReason.Installed, retry.Status);
        Assert.True(retry.RestartRequired);
        await _installer.Received(1).InstallAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>());
        Assert.Single(_saved);
    }

    [Fact]
    public async Task CancellationStopsBeforeActivation()
    {
        await Load(P("A", false, "B"), P("B"));
        _downloader.DownloadAndExtractAsync("https://example.invalid/A", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new OperationCanceledException()));
        Assert.Equal(PackageInstallResultReason.Cancelled, (await _service.InstallAsync("A")).Status);
        Assert.Empty(_activated);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task ConcurrentRootsNeverActivateSharedDependencyTwice()
    {
        await Load(P("A", false, "B"), P("C", false, "B"), P("B"));
        var results = await Task.WhenAll(_service.InstallAsync("A"), _service.InstallAsync("C"));
        Assert.All(results, r => Assert.Equal(PackageInstallResultReason.Installed, r.Status));
        Assert.Equal(1, _activated.Count(x => x == "B"));
    }

    [Fact]
    public async Task RemovedPluginReinstallRequiresRestartEvenAfterRefresh()
    {
        await Load(P("A"));
        await _service.InstallAsync("A");
        Assert.True(await _service.RemoveAsync("A"));
        await Load(P("A"));
        var result = await _service.InstallAsync("A");
        Assert.Equal(PackageInstallResultReason.Installed, result.Status);
        Assert.True(result.RestartRequired);
        Assert.Equal(PackageStatus.NeedRestart, _service.Packages["A"].Status);
        Assert.Equal(["A"], _activated);
        Assert.Single(_saved);
    }

    [Fact]
    public async Task RootCancellationOwnsDependencyDownloadEvenWhenDownloaderReturnsFalse()
    {
        await Load(P("A", false, "B"), P("B"));
        _downloader.DownloadAndExtractAsync("https://example.invalid/B", Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var token = call.Arg<CancellationToken>();
            _service.CancelInstall("B");
            Assert.False(token.IsCancellationRequested);
            _service.CancelInstall("A");
            Assert.True(token.IsCancellationRequested);
            return false;
        });
        var result = await _service.InstallAsync("A");
        Assert.Equal(PackageInstallResultReason.Cancelled, result.Status);
        Assert.Empty(_activated);
        Assert.Empty(_saved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedRootCanBeCancelledWithoutCancellingActiveRoot(bool legacy)
    {
        await Load(P("A"), P("B"));
        var planA = await _service.PlanAsync([new("A")]);
        var planB = await _service.PlanAsync([new("B")]);
        var downloading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _downloader.DownloadAndExtractAsync("https://example.invalid/A", Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            downloading.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            Directory.CreateDirectory(call.ArgAt<string>(1));
            return true;
        });
        var active = _service.ExecuteAsync(planA, []);
        await downloading.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var queued = legacy ? _service.InstallAsync("B") : _service.ExecuteAsync(planB, []);
            _service.CancelInstall("B");
            Assert.Equal(PackageInstallResultReason.Cancelled,
                (await queued.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(PackageInstallResultReason.Installed, (await active).Status);
        Assert.Equal(["A"], _activated);
    }

    [Fact]
    public async Task PreCancelledExecutionReturnsStructuredResultAndDoesNotReleaseUnownedLock()
    {
        await Load(P("A"));
        var plan = await _service.PlanAsync([new("A")]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(PackageInstallResultReason.Cancelled,
            (await _service.ExecuteAsync(plan, [], cancellation.Token)).Status);
        Assert.Equal(PackageInstallResultReason.Installed, (await _service.ExecuteAsync(plan, [])).Status);
    }

    [Fact]
    public async Task QueuedSameRootTakesCancellationOwnershipAfterFailedPredecessor()
    {
        await Load(P("A"));
        var plan = await _service.PlanAsync([new("A")]);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        _downloader.DownloadAndExtractAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<IProgress<float>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (++attempts == 1)
            {
                started.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            else
            {
                _service.CancelInstall("A");
                Assert.True(call.Arg<CancellationToken>().IsCancellationRequested);
            }
            return false;
        });
        var first = _service.ExecuteAsync(plan, []);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var queued = _service.ExecuteAsync(plan, []);
        release.SetResult();
        Assert.Equal(PackageInstallResultReason.ErrorDownloading, (await first).Status);
        Assert.Equal(PackageInstallResultReason.Cancelled, (await queued).Status);
    }

    [Fact]
    public async Task CancellationAfterPersistenceRollsBackBeforeActivation()
    {
        await Load(P("A"));
        _store.SaveAsync(Arg.Any<IEnumerable<InstalledPackage>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _saved = call.Arg<IEnumerable<InstalledPackage>>().ToArray();
            if (_saved.Length > 0) _service.CancelInstall("A");
            return Task.CompletedTask;
        });
        Assert.Equal(PackageInstallResultReason.Cancelled, (await _service.InstallAsync("A")).Status);
        Assert.Empty(_activated);
        Assert.Empty(_saved);
        Assert.Null(_service.Packages["A"].InstalledVersion);
        Assert.False(Directory.Exists(Path.Combine(_folder, "A")));
    }

    [Fact]
    public async Task RemovalWriteFailurePreservesFilesAndDoesNotUnloadPlugin()
    {
        await Load(P("A"));
        await _service.InstallAsync("A");
        _store.SaveAsync(Arg.Any<IEnumerable<InstalledPackage>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var records = call.Arg<IEnumerable<InstalledPackage>>().ToArray();
            if (records.Length == 0) throw new IOException("write failed");
            _saved = records;
            return Task.CompletedTask;
        });
        Assert.False(await _service.RemoveAsync("A"));
        Assert.Equal("1", _service.Packages["A"].InstalledVersion?.Version);
        Assert.True(File.Exists(Path.Combine(_folder, "A", "payload")));
        Assert.Single(_saved);
        await _installer.DidNotReceive().RemoveAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemovalHookFailureRestoresPersistedInstallationAndRequiresRestart()
    {
        await Load(P("A"));
        await _service.InstallAsync("A");
        _installer.RemoveAsync(Arg.Any<PackageInstallContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PackageInstallerResult>(new IOException("partial removal failed")));
        Assert.False(await _service.RemoveAsync("A"));
        Assert.Single(_saved);
        Assert.Equal("1", _service.Packages["A"].InstalledVersion?.Version);
        Assert.Equal(PackageStatus.NeedRestart, _service.Packages["A"].Status);
        Assert.True(File.Exists(Path.Combine(_folder, "A", "payload")));
    }

    [Fact]
    public async Task PendingRestartSurvivesFailedUpdatePreflight()
    {
        await Load(P("A"));
        await _service.InstallAsync("A");
        var next = P("A");
        var targets = next.Versions![0].Targets;
        next.Versions = [new PackageVersion { Version = "2", Targets = targets }];
        await Load(next);
        Assert.True((await _service.UpdateAsync("A")).RestartRequired);
        next.Versions = [new PackageVersion { Version = "3", Targets = targets }];
        await Load(next);
        _installer.CheckCompatibilityAsync(Arg.Any<Package>(), Arg.Any<PackageVersion>(), Arg.Any<CancellationToken>())
            .Returns(new CompatibilityReport(false));
        Assert.Equal(PackageInstallResultReason.Incompatible, (await _service.UpdateAsync("A")).Status);
        Assert.Equal("2", _service.Packages["A"].InstalledVersion?.Version);
        Assert.Equal(PackageStatus.NeedRestart, _service.Packages["A"].Status);
    }

    [Fact]
    public async Task UpdatingUsesPreviousTargetForProcessCleanup()
    {
        var package = P("A");
        await Load(package);
        await _service.InstallAsync("A");
        var oldTarget = package.Versions![0].Targets![0];
        package.Versions = [package.Versions[0], new PackageVersion
            { Version = "2", Targets = [new PackageTarget { Target = "all", Url = "https://example.invalid/new" }] }];
        await Load(package);
        await _service.UpdateAsync("A");
        await _installer.Received().PrepareRemoveAsync(
            Arg.Is<PackageInstallContext>(x => x.Version.Version == "1" && x.Target == oldTarget), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisteringNewManifestDoesNotForgetInstallation()
    {
        await Load(P("A"));
        await _service.InstallAsync("A");
        _service.RegisterPackage(new Package { Id = "A", Type = "Plugin", Versions = [] });
        Assert.Equal("1", _service.Packages["A"].InstalledVersion?.Version);
        Assert.True(await _service.RemoveAsync("A"));
        Assert.Empty(_saved);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("A.")]
    [InlineData("A.backup-123")]
    public async Task UnsafeIdentitiesNeverReachInstaller(string id)
    {
        await Load(P(id));
        Assert.Equal(PackageInstallResultReason.InvalidPlan, (await _service.InstallAsync(id)).Status);
        Assert.False(await _service.RemoveAsync(id));
        Assert.Empty(_activated);
        Assert.False(Directory.Exists(_folder));
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }
}