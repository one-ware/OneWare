using Avalonia.Headless.XUnit;
using OneWare.Essentials.Enums;
using OneWare.PackageManager.ViewModels;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class PackageListBehaviorTests
{
    private static PackageViewModel Vm(string id, string name, PackageStatus status)
    {
        return PackageTestFactory.CreateViewModel(id, name, status);
    }

    [AvaloniaFact]
    public void OrderIsStableAndStatusIndependent()
    {
        var root = new PackageCategoryViewModel("All", PackageCategoryKind.Root);
        var plugins = root.GetOrCreateSubCategory("Plugins");
        var hardware = root.GetOrCreateSubCategory("Hardware");

        var zeta = Vm("p.zeta", "Zeta", PackageStatus.Available);
        var alpha = Vm("p.alpha", "Alpha", PackageStatus.UpdateAvailable);
        var beta = Vm("h.beta", "Beta", PackageStatus.Installed);

        plugins.Add(zeta);
        plugins.Add(alpha);
        hardware.Add(beta);

        root.Relayout(PackageListQuery.Default);

        var entries = root.VisibleEntries.Select(Describe).ToList();

        // separators are categories, packages alphabetical within them
        Assert.Equal(["#Plugins", "Alpha", "Zeta", "#Hardware", "Beta"], entries);

        var before = root.VisibleEntries.ToList();

        // simulate an install: status changes, nothing else
        ((FakeState)alpha.PackageState).Status = PackageStatus.NeedRestart;
        root.Resync();

        Assert.Equal(before, root.VisibleEntries.ToList());
    }

    [AvaloniaFact]
    public void SegmentFilterDoesNotYankRowsOnDataChange()
    {
        var root = new PackageCategoryViewModel("All", PackageCategoryKind.Root);
        var plugins = root.GetOrCreateSubCategory("Plugins");

        var a = Vm("a", "Aaa", PackageStatus.Available);
        var b = Vm("b", "Bbb", PackageStatus.Available);
        plugins.Add(a);
        plugins.Add(b);

        // user selects "Available"
        root.Relayout(new PackageListQuery(string.Empty, false, true));
        Assert.Equal(2, root.VisiblePackages.Count);

        // user installs Aaa -> it is no longer "available" but must stay put
        ((FakeState)a.PackageState).Status = PackageStatus.Installed;
        root.Resync();
        Assert.Equal(2, root.VisiblePackages.Count);
        Assert.Same(a, root.VisiblePackages[0]);

        // next user action drops it
        root.Relayout(new PackageListQuery(string.Empty, false, true));
        Assert.Single(root.VisiblePackages);
        Assert.Same(b, root.VisiblePackages[0]);
    }

    [AvaloniaFact]
    public void SearchMatchesCategoriesAndPackages()
    {
        var root = new PackageCategoryViewModel("All", PackageCategoryKind.Root);
        var plugins = root.GetOrCreateSubCategory("Plugins");
        var hardware = root.GetOrCreateSubCategory("Hardware");

        plugins.Add(Vm("p1", "Verilog", PackageStatus.Available));
        hardware.Add(Vm("h1", "Cyclone", PackageStatus.Available));

        // package name match
        root.Relayout(new PackageListQuery("veri", true, true));
        Assert.True(plugins.IsVisible);
        Assert.False(hardware.IsVisible);
        Assert.Single(root.VisiblePackages);

        // category header match shows all of its packages
        root.Relayout(new PackageListQuery("hardw", true, true));
        Assert.True(hardware.IsVisible);
        Assert.False(plugins.IsVisible);
        Assert.Single(root.VisiblePackages);
        Assert.Equal("Cyclone", root.VisiblePackages[0].PackageState.Package.Name);

        // root is never hidden
        root.Relayout(new PackageListQuery("zzzz", true, true));
        Assert.True(root.IsVisible);
        Assert.Empty(root.VisiblePackages);
    }

    [AvaloniaFact]
    public void SearchRanksExactAndPrefixFirst()
    {
        var root = new PackageCategoryViewModel("All", PackageCategoryKind.Root);
        root.Add(Vm("a", "My Quartus Helper", PackageStatus.Available));
        root.Add(Vm("b", "Quartus", PackageStatus.Available));
        root.Add(Vm("c", "Quartus Prime", PackageStatus.Available));

        root.Relayout(new PackageListQuery("quartus", true, true));

        Assert.Equal(["Quartus", "Quartus Prime", "My Quartus Helper"],
            root.VisiblePackages.Select(x => x.PackageState.Package.Name));
    }

    [AvaloniaFact]
    public void OwnGroupAndSubCategoryWithSameLabelStayStable()
    {
        var plugins = new PackageCategoryViewModel("Plugins");
        var other = plugins.GetOrCreateSubCategory("Other");

        plugins.Add(Vm("direct", "Direct", PackageStatus.Available));
        other.Add(Vm("nested", "Nested", PackageStatus.Available));

        plugins.Relayout(PackageListQuery.Default);

        var separators = plugins.VisibleEntries.OfType<PackageSeparatorViewModel>().ToList();
        Assert.Equal(2, separators.Count);
        Assert.NotSame(separators[0], separators[1]);

        var before = plugins.VisibleEntries.ToList();

        plugins.Resync();

        // No reset, the entries must be the very same instances in the very same order.
        Assert.Equal(before, plugins.VisibleEntries.ToList());
    }

    private static string Describe(PackageListEntryViewModel entry)
    {
        return entry switch
        {
            PackageSeparatorViewModel s => "#" + s.Text,
            PackageViewModel p => p.PackageState.Package.Name!,
            _ => "?"
        };
    }
}
