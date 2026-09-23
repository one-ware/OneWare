using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.PackageManager.ViewModels;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class FeaturedPackageViewModelTests
{
    private static FeaturedPackageViewModel Create()
    {
        return new FeaturedPackageViewModel("Title", "Description", "AI_Img", "https://one-ware.com/one-ai",
            new RelayCommand(() => { }));
    }

    [AvaloniaFact]
    public void HiddenWithoutTarget()
    {
        Assert.False(Create().IsVisible);
    }

    [AvaloniaFact]
    public void VisibleWhileAvailable()
    {
        var featured = Create();
        featured.Target = PackageTestFactory.CreateViewModel("OneWare.AI", "ONE AI", PackageStatus.Available);

        Assert.True(featured.IsVisible);
    }

    [AvaloniaFact]
    public void VisibleWhileInstalling()
    {
        var featured = Create();
        var target = PackageTestFactory.CreateViewModel("OneWare.AI", "ONE AI", PackageStatus.Available);
        featured.Target = target;

        ((FakeState)target.PackageState).Status = PackageStatus.Installing;

        Assert.True(featured.IsVisible);
    }

    [AvaloniaFact]
    public void HiddenOnceInstalled()
    {
        var featured = Create();
        var target = PackageTestFactory.CreateViewModel("OneWare.AI", "ONE AI", PackageStatus.Available);
        featured.Target = target;

        var raised = 0;
        featured.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FeaturedPackageViewModel.IsVisible)) raised++;
        };

        ((FakeState)target.PackageState).Status = PackageStatus.Installed;

        Assert.False(featured.IsVisible);
        Assert.True(raised > 0);
    }

    [AvaloniaFact]
    public void SwappingTargetDetachesTheOldHandler()
    {
        var featured = Create();
        var stale = PackageTestFactory.CreateViewModel("OneWare.AI", "ONE AI", PackageStatus.Available);
        featured.Target = stale;

        var current = PackageTestFactory.CreateViewModel("OneWare.AI", "ONE AI", PackageStatus.Installed);
        featured.Target = current;

        var raised = 0;
        featured.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FeaturedPackageViewModel.IsVisible)) raised++;
        };

        // the orphaned view model must no longer be able to change the banner
        ((FakeState)stale.PackageState).Status = PackageStatus.Installing;

        Assert.Equal(0, raised);
        Assert.False(featured.IsVisible);
    }

    [AvaloniaFact]
    public async Task PrimaryCommandResolvesTabsBeforeRunningTheAction()
    {
        var featured = Create();
        var target = PackageTestFactory.CreateViewModel("OneWare.AI", "ONE AI", PackageStatus.Available);
        featured.Target = target;

        var executed = false;
        target.MainButtonCommand = new RelayCommand(() => executed = true);

        await featured.PrimaryCommand.ExecuteAsync(null);

        // the banner never selects the row, so it has to resolve the tabs the install flow depends on
        Assert.True(target.IsTabsResolved);
        Assert.True(executed);
    }

    [AvaloniaFact]
    public async Task PrimaryCommandIsANoOpWithoutTarget()
    {
        await Create().PrimaryCommand.ExecuteAsync(null);
    }

    [AvaloniaFact]
    public void DisposeDetachesTheTarget()    {
        var featured = Create();
        var target = PackageTestFactory.CreateViewModel("OneWare.AI", "ONE AI", PackageStatus.Available);
        featured.Target = target;

        featured.Dispose();

        var raised = 0;
        featured.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FeaturedPackageViewModel.IsVisible)) raised++;
        };

        ((FakeState)target.PackageState).Status = PackageStatus.Installed;

        Assert.Equal(0, raised);
    }
}
