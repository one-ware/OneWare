using System;
using System.IO;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Xunit;

namespace OneWare.Essentials.UnitTests;

[CollectionDefinition("Language server lifecycle", DisableParallelization = true)]
public class LanguageServerLifecycleCollection;

[Collection("Language server lifecycle")]
public class LanguageServiceLspAutoDownloadTests : IDisposable
{
    private readonly string _executable = Path.GetTempFileName();
    private readonly IServiceProvider? _previousContainer = ContainerLocator.Container;

    public LanguageServiceLspAutoDownloadTests()
    {
        ContainerLocator.SetContainer(new LoggerProvider());
    }

    [Fact]
    public async Task InitialPathDoesNotActivateBeforeDerivedConstruction()
    {
        using var path = new BehaviorSubject<string>(_executable);
        var server = CreateServer(path, out var packages);

        Assert.Equal(0, server.Starts);
        Assert.Equal("lsp", server.StartArguments);
        await server.ActivateAsync();
        Assert.Equal(1, server.Starts);
        Assert.Equal(0, packages.Installs);
    }

    [Fact]
    public async Task ConcurrentActivationAndInstallAutoSettingLaunchOnlyOnce()
    {
        using var path = new BehaviorSubject<string>("");
        var server = CreateServer(path, out var packages);
        var install = new TaskCompletionSource<PackageInstallResult>();
        packages.Install = () => install.Task;

        var first = server.ActivateAsync();
        await server.ActivateAsync();
        path.OnNext(_executable);
        install.SetResult(new PackageInstallResult { Status = PackageInstallResultReason.Installed });
        await first;

        Assert.Equal(1, packages.Installs);
        Assert.Equal(1, server.Starts);
    }

    [Theory]
    [InlineData(PackageInstallResultReason.ErrorDownloading)]
    [InlineData(PackageInstallResultReason.Incompatible)]
    [InlineData(PackageInstallResultReason.NotFound)]
    public async Task FailedInstallDoesNotActivateOrRepeatedlyDownloadAndAllowsManualRecovery(
        PackageInstallResultReason reason)
    {
        using var path = new BehaviorSubject<string>("");
        var server = CreateServer(path, out var packages);
        packages.Install = () => Task.FromResult(new PackageInstallResult { Status = reason });

        await server.ActivateAsync();
        await server.ActivateAsync();
        Assert.False(server.IsActivated);
        Assert.Equal(0, server.Starts);
        Assert.Equal(1, packages.Installs);

        path.OnNext(_executable);
        Assert.Equal(1, server.Starts);
    }

    [Fact]
    public async Task DisabledAutoDownloadNeverInstalls()
    {
        using var path = new BehaviorSubject<string>("");
        var server = CreateServer(path, out var packages, false);
        await server.ActivateAsync();
        Assert.Equal(0, packages.Installs);
    }

    [Fact]
    public async Task RestartBlocksNewActivationUntilTheOutgoingStopCompletes()
    {
        using var path = new BehaviorSubject<string>(_executable);
        var server = CreateServer(path, out _);
        var exited = new TaskCompletionSource();
        var stopped = new TaskCompletionSource();
        var replacementStarted = new TaskCompletionSource();
        server.Launch = () =>
        {
            if (server.Starts == 2) replacementStarted.SetResult();
            return exited.Task;
        };
        server.Stop = () =>
        {
            exited.TrySetResult();
            return stopped.Task;
        };
        var initial = server.ActivateAsync();

        var restart = server.RestartAsync();
        await initial;
        await server.ActivateAsync();
        Assert.Equal(1, server.Starts);

        exited = new TaskCompletionSource();
        stopped.SetResult();
        await replacementStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, server.Starts);
        await server.DeactivateAsync();
        await restart.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(server.IsActivated);
    }

    [Fact]
    public async Task LaterDeactivationCancelsPendingRestart()
    {
        using var path = new BehaviorSubject<string>(_executable);
        var server = CreateServer(path, out _);
        var exited = new TaskCompletionSource();
        var stopped = new TaskCompletionSource();
        server.Launch = () => exited.Task;
        server.Stop = () =>
        {
            exited.TrySetResult();
            return stopped.Task;
        };
        var initial = server.ActivateAsync();
        var restart = server.RestartAsync();
        await initial;
        var laterStop = server.DeactivateAsync();

        stopped.SetResult();
        await laterStop;
        await restart.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, server.Starts);
        Assert.False(server.IsActivated);
    }

    [Fact]
    public async Task DeactivationDuringInstallationPreventsLateLaunch()
    {
        using var path = new BehaviorSubject<string>("");
        var server = CreateServer(path, out var packages);
        var install = new TaskCompletionSource<PackageInstallResult>();
        packages.Install = () => install.Task;
        var activation = server.ActivateAsync();

        await server.DeactivateAsync();
        path.OnNext(_executable);
        install.SetResult(new PackageInstallResult { Status = PackageInstallResultReason.Installed });
        await activation;
        Assert.Equal(0, server.Starts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PackageReplacementReactivatesBeforeOrAfterOutgoingCleanup(bool publishBeforeCleanup)
    {
        using var path = new BehaviorSubject<string>(_executable);
        var server = CreateServer(path, out _);
        var exited = new TaskCompletionSource();
        server.Launch = () => server.Starts == 1 ? exited.Task : Task.CompletedTask;
        var initial = server.ActivateAsync();
        server.SimulateNaturalExitCleanup();

        if (!publishBeforeCleanup)
        {
            exited.SetResult();
            await initial;
        }
        path.OnNext("");
        path.OnNext(_executable);
        if (publishBeforeCleanup)
        {
            Assert.Equal(1, server.Starts);
            exited.SetResult();
            await initial;
        }

        Assert.Equal(2, server.Starts);
    }

    [Fact]
    public async Task ExplicitStopInvalidatesQueuedPackageReplacement()
    {
        using var path = new BehaviorSubject<string>(_executable);
        var server = CreateServer(path, out _);
        var exited = new TaskCompletionSource();
        server.Launch = () => exited.Task;
        var initial = server.ActivateAsync();
        server.SimulateNaturalExitCleanup();
        path.OnNext("");
        path.OnNext(_executable);

        await server.DeactivateAsync();
        exited.SetResult();
        await initial;

        Assert.Equal(1, server.Starts);
        Assert.False(server.IsActivated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/a-nonexistent-oneware-language-server/executable")]
    public async Task MissingExecutableDoesNotLeaveBaseServiceActivated(string path)
    {
        var server = new MissingServer(path);
        await server.ActivateAsync();
        Assert.False(server.IsActivated);
        Assert.False(server.IsLanguageServiceReady);
    }

    private static TestServer CreateServer(BehaviorSubject<string> path, out PackageProxy packages,
        bool autoDownload = true)
    {
        var service = DispatchProxy.Create<IPackageService, PackageProxy>();
        packages = (PackageProxy)service;
        return new TestServer(path, service, new BehaviorSubject<bool>(autoDownload));
    }

    public void Dispose()
    {
        File.Delete(_executable);
        if (_previousContainer != null) ContainerLocator.SetContainer(_previousContainer);
    }

    public class PackageProxy : DispatchProxy
    {
        public int Installs { get; private set; }
        public Func<Task<PackageInstallResult>> Install { get; set; } = () =>
            Task.FromResult(new PackageInstallResult { Status = PackageInstallResultReason.Installed });

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IPackageService.InstallAsync))
                throw new NotSupportedException(targetMethod?.Name);
            Installs++;
            return Install();
        }
    }

    private sealed class LoggerProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ILogger) ? NullLogger.Instance : null;
    }

    private sealed class TestServer(IObservable<string> path, IPackageService packages, IObservable<bool> enabled)
        : LanguageServiceLspAutoDownload(path, new Package { Id = "test" }, "test", null, packages, enabled, "lsp")
    {
        public int Starts { get; private set; }
        public string? StartArguments => Arguments;
        public Func<Task>? Launch { get; set; }
        public Func<Task>? Stop { get; set; }
        public void SimulateNaturalExitCleanup() => IsActivated = false;

        protected override Task ActivateServerAsync()
        {
            Starts++;
            IsActivated = Launch != null;
            return Launch?.Invoke() ?? Task.CompletedTask;
        }

        protected override Task DeactivateServerAsync()
        {
            IsActivated = false;
            return Stop?.Invoke() ?? Task.CompletedTask;
        }

        public override ITypeAssistance GetTypeAssistance(IEditor editor) => throw new NotSupportedException();
    }

    private sealed class MissingServer : LanguageServiceLsp
    {
        public MissingServer(string path) : base("missing", null) => ExecutablePath = path;
        public override ITypeAssistance GetTypeAssistance(IEditor editor) => throw new NotSupportedException();
    }
}
