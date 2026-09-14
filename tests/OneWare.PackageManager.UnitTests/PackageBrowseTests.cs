using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.ViewModels;
using OneWare.PackageManager.Views;
using OneWare.PackageManager.Services;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(OneWare.PackageManager.UnitTests.PackageTestAppBuilder))]

namespace OneWare.PackageManager.UnitTests;

public sealed class PackageTestApp : Application
{
    public override void Initialize() => Styles.Add(new Avalonia.Themes.Simple.SimpleTheme());
}
public static class PackageTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<PackageTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class PackageBrowseTests
{
    private static IPackageState State(string id, PackageStatus status)
    {
        var state = Substitute.For<IPackageState>();
        state.Package.Returns(new Package { Id = id, Name = id, Type = "Plugin", Category = "Tools", Description = "Computer vision", Versions = [new PackageVersion { Version = "1" }] });
        state.Status.Returns(status);
        if (status != PackageStatus.Available) state.InstalledVersion.Returns(new PackageVersion { Version = "0.5" });
        return state;
    }

    private static (PackageManagerViewModel Vm, IPackageService Service) Create(IWindowService? windows = null, IHttpService? http = null)
    {
        var service = Substitute.For<IPackageService, IPackageDiscoveryService>();
        ((IPackageDiscoveryService)service).FeaturedPackageIds.Returns(new[] { "OneWare.AI" });
        var packages = new Dictionary<string, IPackageState> { ["OneWare.AI"] = State("OneWare.AI", PackageStatus.Available),
            ["Other"] = State("Other", PackageStatus.UpdateAvailable) };
        service.Packages.Returns(packages);
        service.CheckCompatibilityAsync(Arg.Any<string>(), Arg.Any<PackageVersion>()).Returns(new CompatibilityReport(true));
        return (new PackageManagerViewModel(service, http ?? Substitute.For<IHttpService>(), Substitute.For<ILogger>(),
            windows ?? Substitute.For<IWindowService>(), Substitute.For<IApplicationStateService>()), service);
    }

    [AvaloniaFact]
    public async Task DeepLinkIgnoresFiltersAndBackRestoresBrowseState()
    {
        var (vm, _) = Create();
        using var lifetime = vm;
        vm.Filter = "Other";
        vm.SelectedFilterIndex = 1;
        vm.CategoryFilter = "Plugin";
        Assert.Single(vm.BrowsePackages);
        await vm.OpenDependencyCommand.ExecuteAsync("OneWare.AI");
        Assert.True(vm.IsDetails);
        Assert.Equal("OneWare.AI", vm.SelectedPackage!.PackageState.Package.Id);
        vm.BackCommand.Execute(null);
        Assert.False(vm.IsDetails);
        Assert.Equal("Other", vm.Filter);
        Assert.Equal(1, vm.SelectedFilterIndex);
        Assert.Equal("Plugin", vm.CategoryFilter);
        Assert.Single(vm.BrowsePackages);
    }

    [AvaloniaFact]
    public void FeaturedHidesWithFiltersAndUpdatesCountMatches()
    {
        var (vm, _) = Create();
        using var lifetime = vm;
        Assert.True(vm.ShowFeatured);
        Assert.Equal(1, vm.UpdateCount);
        vm.Filter = "vision";
        vm.CategoryFilter = "Tools";
        Assert.False(vm.ShowFeatured);
        Assert.Equal(2, vm.BrowsePackages.Count);
        vm.SelectedFilterIndex = 2;
        Assert.Single(vm.BrowsePackages);
        Assert.Equal("Other", vm.BrowsePackages[0].PackageState.Package.Id);
    }

    [AvaloniaFact]
    public void RefreshReusesViewModelsAndDetachesOldState()
    {
        var (vm, service) = Create();
        using var lifetime = vm;
        var original = vm.BrowsePackages.Single(x => x.PackageState.Package.Id == "OneWare.AI");
        var oldState = original.PackageState;
        var replacement = State("OneWare.AI", PackageStatus.Installed);
        service.Packages.Returns(new Dictionary<string, IPackageState> { ["OneWare.AI"] = replacement });
        service.PackagesUpdated += Raise.Event<EventHandler>(service, EventArgs.Empty);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(original, Assert.Single(vm.BrowsePackages));
        Assert.Same(replacement, original.PackageState);
        Assert.Equal("Installed", original.PrimaryButtonText);
        Assert.Null(original.MainButtonCommand);
        oldState.Status.Returns(PackageStatus.Installing);
        oldState.PropertyChanged += Raise.Event<System.ComponentModel.PropertyChangedEventHandler>(oldState,
            new System.ComponentModel.PropertyChangedEventArgs("Status"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Installed", original.PrimaryButtonText);
        Assert.Equal(0, vm.UpdateCount);
    }

    [AvaloniaFact]
    public void NewViewLoadsWithCompiledBindings()
    {
        var (vm, _) = Create();
        using var lifetime = vm;
        var view = new PackageManagerView { DataContext = vm };
        view.Measure(new Size(1000, 700));
        view.Arrange(new Rect(0, 0, 1000, 700));
        Assert.Equal("Package Manager", view.Title);
    }

    [AvaloniaFact]
    public async Task SearchDebouncesAndClearPreservesPageAndCategory()
    {
        var (vm, _) = Create();
        using (vm)
        {
            Dispatcher.UIThread.RunJobs();
            var changed = new TaskCompletionSource();
            vm.BrowsePackages.CollectionChanged += (_, _) => changed.TrySetResult();
            vm.Filter = "does not exist";
            Assert.False(vm.ShowFeatured); // Promotions disappear immediately, not after the debounce.
            Assert.Equal(2, vm.BrowsePackages.Count);
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(vm.BrowsePackages);
            vm.SelectedFilterIndex = 1;
            vm.CategoryFilter = "Plugins/Tools";
            vm.ClearSearchCommand.Execute(null);
            Assert.Equal(1, vm.SelectedFilterIndex);
            Assert.Equal("Plugins/Tools", vm.CategoryFilter);
            Assert.Single(vm.BrowsePackages);
        }
    }

    [Fact]
    public void SearchRanksExactIdAndRequiresAllTokensAcrossMetadata()
    {
        var exact = new Package { Id = "vision", Name = "Vision" };
        var named = new Package { Id = "other", Name = "Vision tools" };
        var description = new Package { Name = "Analyzer", Description = "Computer vision", Category = "Tools" };
        Assert.True(PackageSearch.Score(exact, "VISION") < PackageSearch.Score(named, "vision"));
        Assert.True(PackageSearch.Score(named, "vision") < PackageSearch.Score(description, "vision"));
        Assert.NotNull(PackageSearch.Score(description, "COMPUTER tools"));
        Assert.Null(PackageSearch.Score(description, "vision missing"));
    }

    [AvaloniaFact]
    public void RegisteredNestedCategoriesAndInstalledOnlyPackagesRemainDiscoverable()
    {
        var (vm, service) = Create();
        using (vm)
        {
            vm.RegisterCategory("Plugins/Company/Analysis");
            Assert.Contains("Plugins/Company/Analysis", vm.CategoryFilters);
            var state = State("Offline", PackageStatus.Unavailable);
            state.Package.Returns(new Package { Id = "Offline", Name = "Offline", Type = "Plugin", Category = "Company/Analysis" });
            service.Packages.Returns(new Dictionary<string, IPackageState> { ["Offline"] = state });
            service.PackagesUpdated += Raise.Event<EventHandler>(service, EventArgs.Empty);
            Dispatcher.UIThread.RunJobs();
            vm.SelectedFilterIndex = 1;
            var package = Assert.Single(vm.BrowsePackages);
            Assert.Equal("Installed", package.PrimaryButtonText);
            Assert.Null(package.MainButtonCommand);
            Assert.True(package.RemoveCommand.CanExecute(null));
            Assert.False(vm.ShowFeatured);
        }
    }

    [AvaloniaFact]
    public void StatusNotificationsDoNotResetUnchangedBrowseRows()
    {
        var (vm, service) = Create();
        using (vm)
        {
            Dispatcher.UIThread.RunJobs();
            var changes = 0;
            vm.BrowsePackages.CollectionChanged += (_, _) => changes++;
            service.Packages["Other"].PropertyChanged += Raise.Event<System.ComponentModel.PropertyChangedEventHandler>(
                service.Packages["Other"], new System.ComponentModel.PropertyChangedEventArgs("Status"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, changes);
            Assert.True(vm.UpdateAllCommand.CanExecute(null));
            vm.IsLoading = true;
            Assert.False(vm.UpdateAllCommand.CanExecute(null));
            Assert.False(vm.ShowEmptyState);
        }
    }

    [AvaloniaFact]
    public async Task RefreshFailureIsVisibleAndRetainsInstalledRows()
    {
        var (vm, service) = Create();
        using (vm)
        {
            service.RefreshAsync(false).Returns(Task.FromException<bool>(new IOException("Offline")));
            await vm.RefreshPackagesAsync();
            Assert.Contains("Sources could not be loaded", vm.SourceWarning);
            vm.SelectedFilterIndex = 1;
            Assert.Single(vm.BrowsePackages);
        }
    }

    [AvaloniaFact]
    public async Task DependenciesShowBoundsAndInstalledSatisfactionAndDeepLink()
    {
        var (vm, service) = Create();
        using (vm)
        {
            var root = State("Root", PackageStatus.Available);
            root.Package.Returns(new Package { Id = "Root", Name = "Root", Type = "Plugin", Versions =
                [new PackageVersion { Version = "1", Dependencies = [new PackageDependency { Id = "Other", MinVersion = "1", MaxVersionExclusive = "2" }] }] });
            var packages = new Dictionary<string, IPackageState> { ["Root"] = root, ["Other"] = service.Packages["Other"] };
            service.Packages.Returns(packages);
            service.PackagesUpdated += Raise.Event<EventHandler>(service, EventArgs.Empty);
            Dispatcher.UIThread.RunJobs();
            await vm.OpenDependencyCommand.ExecuteAsync("Root");
            var dependency = Assert.Single(vm.SelectedPackage!.Dependencies);
            Assert.Equal("≥ 1, < 2", dependency.Requirement);
            Assert.Contains("Installed 0.5", dependency.Status);
            Assert.Contains("version change required", dependency.Status);
            Assert.True(dependency.CanOpen);
            await vm.OpenDependencyCommand.ExecuteAsync(dependency.Id);
            Assert.Equal("Other", vm.SelectedPackage!.PackageState.Package.Id);
        }
    }

    [AvaloniaFact]
    public async Task CompatibilityCompletionStaysWithTheRequestedVersion()
    {
        var service = Substitute.For<IPackageService>();
        var state = State("Root", PackageStatus.Available);
        state.Package.Returns(new Package { Id = "Root", Type = "Plugin", Versions =
            [new PackageVersion { Version = "1" }, new PackageVersion { Version = "2" }] });
        var first = new TaskCompletionSource<CompatibilityReport>();
        var second = new TaskCompletionSource<CompatibilityReport>();
        service.CheckCompatibilityAsync("Root", Arg.Is<PackageVersion>(x => x.Version == "2")).Returns(first.Task);
        service.CheckCompatibilityAsync("Root", Arg.Is<PackageVersion>(x => x.Version == "1")).Returns(second.Task);
        using var vm = new PackageViewModel(state, service, Substitute.For<IHttpService>(), Substitute.For<IWindowService>(),
            Substitute.For<IApplicationStateService>(), Substitute.For<ILogger>());
        var initial = vm.SelectedVersionModel!;
        vm.SelectedVersionModel = vm.PackageVersionModels.Single(x => x.Version.Version == "1");
        first.SetResult(new CompatibilityReport(false));
        await first.Task;
        Dispatcher.UIThread.RunJobs();
        Assert.Null(vm.SelectedVersionModel.CompatibilityReport);
        Assert.False(initial.CompatibilityReport!.IsCompatible);
        second.SetResult(new CompatibilityReport(true));
        await second.Task;
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.SelectedVersionModel.CompatibilityReport!.IsCompatible);
        Assert.False(vm.IsCompatibilityChecking);
    }

    [AvaloniaFact]
    public async Task FailedTabDownloadsFinishAndDoNotControlLicenseReview()
    {
        var http = Substitute.For<IHttpService>();
        http.DownloadTextAsync(Arg.Any<string>()).Returns(Task.FromException<string?>(new IOException("Offline")));
        var (vm, service) = Create(http: http);
        using (vm)
        {
            var state = State("Root", PackageStatus.Available);
            state.Package.Returns(new Package { Id = "Root", Type = "Plugin", Description = "About root", License = "MIT",
                Tabs = [new PackageTab { Title = "Changelog", ContentUrl = "https://example.invalid/changelog" }] });
            service.Packages.Returns(new Dictionary<string, IPackageState> { ["Root"] = state });
            service.PackagesUpdated += Raise.Event<EventHandler>(service, EventArgs.Empty);
            Dispatcher.UIThread.RunJobs();
            await vm.OpenDependencyCommand.ExecuteAsync("Root");
            await vm.SelectedPackage!.ResolveTabsAsync();
            Assert.True(vm.SelectedPackage.IsTabsResolved);
            Assert.Contains(vm.SelectedPackage.Tabs, x => x.Title == "About" && x.Content == "About root");
            Assert.Contains(vm.SelectedPackage.Tabs, x => x.Title == "Changelog" && x.Content.Contains("unavailable"));
            Assert.Contains(vm.SelectedPackage.Tabs, x => x.Title == "License");
            Assert.DoesNotContain(service.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IPackageService.DownloadLicenseAsync));
        }
    }

    private static (IPackageService Service, IPackageOperationService Operations, IWindowService Windows, PackageOperationPlan Plan) ReviewFixture(bool license = true)
    {
        var service = Substitute.For<IPackageService, IPackageOperationService>();
        var operations = (IPackageOperationService)service;
        var root = State("Root", PackageStatus.Available);
        var dependency = State("Dependency", PackageStatus.Available);
        service.Packages.Returns(new Dictionary<string, IPackageState> { ["Root"] = root, ["Dependency"] = dependency });
        var plan = new PackageOperationPlan("test", [new("Root")],
            [new("Dependency", "Dependency", "1", null, PackagePlanAction.Install, license, []),
                new("Root", "Root", "1", null, PackagePlanAction.Install, license, [new PackageDependency { Id = "Dependency" }])], []);
        operations.PlanAsync(Arg.Any<IReadOnlyList<PackageRequest>>(), Arg.Any<CancellationToken>()).Returns(plan);
        operations.ExecuteAsync(plan, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new PackageInstallResult { Status = PackageInstallResultReason.Installed });
        service.DownloadLicenseAsync(root.Package).Returns("Root license");
        service.DownloadLicenseAsync(dependency.Package).Returns("Dependency license");
        var windows = Substitute.For<IWindowService>();
        windows.ShowMessageBoxAsync(Arg.Any<MessageBoxRequest>(), Arg.Any<Window?>()).Returns(new MessageBoxResult
            { Button = new MessageBoxButton { Role = MessageBoxButtonRole.Yes } });
        return (service, operations, windows, plan);
    }

    [AvaloniaFact]
    public async Task SameStateManifestRefreshDiscardsStaleTabCompletion()
    {
        var http = Substitute.For<IHttpService>();
        var download = new TaskCompletionSource<string?>();
        http.DownloadTextAsync("https://example.invalid/old").Returns(download.Task);
        var (vm, service) = Create(http: http);
        using (vm)
        {
            var state = service.Packages["OneWare.AI"];
            state.Package.Returns(new Package { Id = "OneWare.AI", Type = "Plugin", Description = "Old manifest",
                Versions = [new PackageVersion { Version = "1" }],
                Tabs = [new PackageTab { Title = "Old", ContentUrl = "https://example.invalid/old" }] });
            service.PackagesUpdated += Raise.Event<EventHandler>(service, EventArgs.Empty);
            Dispatcher.UIThread.RunJobs();
            await vm.OpenDependencyCommand.ExecuteAsync("OneWare.AI");
            var package = vm.SelectedPackage!;
            var oldLoad = package.ResolveTabsAsync();
            Assert.False(oldLoad.IsCompleted);
            state.Package.Returns(new Package { Id = "OneWare.AI", Type = "Plugin", Description = "New manifest",
                Versions = [new PackageVersion { Version = "2" }] });
            service.PackagesUpdated += Raise.Event<EventHandler>(service, EventArgs.Empty);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(package, vm.SelectedPackage);
            Assert.Equal("2", package.SelectedVersionModel!.Version.Version);
            download.SetResult("Stale download");
            await oldLoad;
            Assert.True(package.IsTabsResolved);
            Assert.Contains(package.Tabs, x => x.Title == "About" && x.Content == "New manifest");
            Assert.DoesNotContain(package.Tabs, x => x.Title == "Old");
        }
    }

    [AvaloniaFact]
    public async Task QuickInstallDeclineIsCancellationNotAnError()
    {
        var (service, operations, windows, _) = ReviewFixture();
        windows.ShowMessageBoxAsync(Arg.Any<MessageBoxRequest>(), Arg.Any<Window?>()).Returns(MessageBoxResult.Canceled());
        var vm = new PackageQuickInstallViewModel(service.Packages["Root"], service, windows);
        await vm.InstallCommand.ExecuteAsync(null);
        Assert.False(vm.Success);
        Assert.False(vm.IsInstalling);
        Assert.Contains("Cancelled", vm.ResultMessage);
        Assert.DoesNotContain(windows.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IWindowService.ShowMessageAsync));
        Assert.DoesNotContain(operations.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IPackageOperationService.ExecuteAsync));
    }

    [AvaloniaFact]
    public async Task LicenseDownloadExceptionIsVisibleWithoutExecution()
    {
        var (service, operations, windows, plan) = ReviewFixture();
        var dependency = service.Packages["Dependency"].Package;
        service.DownloadLicenseAsync(dependency).Returns(Task.FromException<string?>(new IOException("License host offline")));
        var result = await PackageOperationReview.RunAsync(service, windows, plan.Roots);
        Assert.Contains("License host offline", result.Message);
        await windows.Received(1).ShowMessageAsync(Arg.Any<string>(), Arg.Is<string>(x => x.Contains("License host offline")), MessageBoxIcon.Warning, Arg.Any<Window?>());
        Assert.DoesNotContain(operations.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IPackageOperationService.ExecuteAsync));
    }

    [AvaloniaFact]
    public async Task ReviewLoadsEveryRequiredLicenseWithoutOpeningDetails()
    {
        var (service, operations, windows, plan) = ReviewFixture();
        var result = await PackageOperationReview.RunAsync(service, windows, plan.Roots);
        Assert.Equal(PackageInstallResultReason.Installed, result.Status);
        await operations.Received(1).ExecuteAsync(plan,
            Arg.Is<IReadOnlyCollection<string>>(x => x.Count == 2 && x.Contains("Root") && x.Contains("Dependency")), Arg.Any<CancellationToken>());
        await windows.Received(1).ShowMessageBoxAsync(Arg.Is<MessageBoxRequest>(x =>
            x.Message.Contains("Root license") && x.Message.Contains("Dependency license") && x.Message.Contains("required by Root")), Arg.Any<Window?>());
    }

    [AvaloniaFact]
    public async Task MissingOrDeclinedRequiredLicenseNeverExecutes()
    {
        var (service, operations, windows, plan) = ReviewFixture();
        var dependency = service.Packages["Dependency"].Package;
        service.DownloadLicenseAsync(dependency).Returns((string?)null);
        var result = await PackageOperationReview.RunAsync(service, windows, plan.Roots);
        Assert.Equal(PackageInstallResultReason.ConsentRequired, result.Status);
        Assert.DoesNotContain(operations.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IPackageOperationService.ExecuteAsync));
        await windows.Received(1).ShowMessageAsync(Arg.Any<string>(), Arg.Is<string>(x => x.Contains("could not be loaded")), MessageBoxIcon.Warning, Arg.Any<Window?>());
        service.DownloadLicenseAsync(dependency).Returns("Dependency license");
        windows.ShowMessageBoxAsync(Arg.Any<MessageBoxRequest>(), Arg.Any<Window?>()).Returns(MessageBoxResult.Canceled());
        result = await PackageOperationReview.RunAsync(service, windows, plan.Roots);
        Assert.Equal(PackageInstallResultReason.Cancelled, result.Status);
        Assert.DoesNotContain(operations.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(IPackageOperationService.ExecuteAsync));
    }

    [AvaloniaFact]
    public async Task QuickInstallShowsOneDetailedFailureAndDoesNotBypassPlanning()
    {
        var (service, operations, windows, plan) = ReviewFixture(false);
        operations.ExecuteAsync(plan, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(
            new PackageInstallResult { Status = PackageInstallResultReason.PlanChanged, Message = "Review again",
                CompletedPackages = ["Dependency"], RestartRequired = true });
        var vm = new PackageQuickInstallViewModel(service.Packages["Root"], service, windows);
        var command = vm.InstallCommand;
        Assert.Same(command, vm.InstallCommand);
        await command.ExecuteAsync(null);
        Assert.False(vm.Success);
        Assert.False(vm.IsInstalling);
        Assert.Contains("Review again", vm.ResultMessage);
        Assert.Contains("Completed and retained: Dependency", vm.ResultMessage);
        Assert.Contains("Restart required", vm.ResultMessage);
        await windows.Received(1).ShowMessageAsync(Arg.Any<string>(), Arg.Any<string>(), MessageBoxIcon.Warning, Arg.Any<Window?>());
        Assert.DoesNotContain(service.ReceivedCalls(), x => x.GetMethodInfo().Name is "InstallAsync" or "UpdateAsync");
    }

    [AvaloniaFact]
    public async Task MissingPlanningServiceIsVisibleAndNeverFallsBackToInstall()
    {
        var service = Substitute.For<IPackageService>();
        var windows = Substitute.For<IWindowService>();
        var result = await PackageOperationReview.RunAsync(service, windows, [new("Root")]);
        Assert.Equal(PackageInstallResultReason.InvalidPlan, result.Status);
        await windows.Received(1).ShowMessageAsync(Arg.Any<string>(), Arg.Is<string>(x => x.Contains("planning is unavailable")), MessageBoxIcon.Warning, Arg.Any<Window?>());
        Assert.DoesNotContain(service.ReceivedCalls(), x => x.GetMethodInfo().Name is "InstallAsync" or "UpdateAsync");
    }

    [AvaloniaFact]
    public async Task PendingOptionalIconDoesNotBlockQuickInstallReview()
    {
        var (service, _, windows, _) = ReviewFixture(false);
        var icon = new TaskCompletionSource<Avalonia.Media.IImage?>();
        service.DownloadPackageIconAsync(Arg.Any<Package>()).Returns(icon.Task);
        var vm = new PackageQuickInstallViewModel(service.Packages["Root"], service, windows);
        Assert.True(vm.IsLoading);
        Assert.True(vm.InstallCommand.CanExecute(null));
        await vm.InstallCommand.ExecuteAsync(null);
        Assert.True(vm.Success);
        icon.SetResult(null);
    }

    [AvaloniaFact]
    public async Task BackRestoresActualScrollAndSelectionDoesNotNavigate()
    {
        var (vm, service) = Create();
        using (vm)
        {
            var states = Enumerable.Range(0, 80).Select(i => State($"Package{i:00}", PackageStatus.Available)).ToArray();
            var packages = states.ToDictionary(x => x.Package.Id!, x => x);
            service.Packages.Returns(packages);
            service.PackagesUpdated += Raise.Event<EventHandler>(service, EventArgs.Empty);
            Dispatcher.UIThread.RunJobs();
            var view = new PackageManagerView { DataContext = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var list = view.FindControl<ListBox>("PluginList")!;
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.Offset = new Vector(0, 300);
                Dispatcher.UIThread.RunJobs();
                var offset = scroll.Offset;
                Assert.True(offset.Y > 0);
                list.SelectedItem = vm.BrowsePackages[4];
                Assert.False(vm.IsDetails);
                await vm.OpenDependencyCommand.ExecuteAsync("Package04");
                Dispatcher.UIThread.RunJobs();
                vm.BackCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(offset.Y, scroll.Offset.Y, 1);
            }
            finally { window.Close(); }
        }
    }
}