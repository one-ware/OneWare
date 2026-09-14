using OneWare.Essentials.PackageManager;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class InstalledPluginGraphTests
{
    private static InstalledPackage P(string id, params PackageDependency[] dependencies) => new(id, "Plugin", id, null, null, null, "1") { Dependencies = dependencies };

    [Fact]
    public void OfflineGraphOrdersDependenciesAndRetainsLegacyPlugins()
    {
        var legacy = new InstalledPackage("Legacy", "Plugin", "Legacy", null, null, null, "1");
        var result = InstalledPluginGraph.Resolve([P("A", new PackageDependency { Id = "B" }), P("B"), legacy], _ => true);
        Assert.Equal(["B", "A", "Legacy"], result.Packages.Select(x => x.Id));
        Assert.Empty(result.Blocked);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "2")]
    public void MissingDirectoryOrWrongVersionBlocksOnlyDependents(bool directoryExists, string? minimum)
    {
        var result = InstalledPluginGraph.Resolve([P("A", new PackageDependency { Id = "B", MinVersion = minimum }), P("B"), P("Other")],
            id => id != "B" || directoryExists);
        Assert.Contains("A", result.Blocked.Keys);
        Assert.Contains(result.Packages, x => x.Id == "Other");
    }

    [Fact]
    public void CyclesAreBlockedWithoutFallback()
    {
        var result = InstalledPluginGraph.Resolve([P("A", new PackageDependency { Id = "B" }), P("B", new PackageDependency { Id = "A" }), P("Other")], _ => true);
        Assert.Equal(["Other"], result.Packages.Select(x => x.Id));
        Assert.Equal(2, result.Blocked.Count);
    }

    [Theory]
    [InlineData("A.stage-deadbeef")]
    [InlineData("A.BACKUP-deadbeef")]
    public void CrashArtifactsAreNeitherManagedNorLegacyPlugins(string name)
    {
        Assert.False(InstalledPluginGraph.ShouldDiscoverLegacyDirectory(name, []));
        var result = InstalledPluginGraph.Resolve([P(name), P("Other")], _ => true);
        Assert.Equal(["Other"], result.Packages.Select(x => x.Id));
        Assert.Contains(name, result.Blocked.Keys);
    }

    [Fact]
    public void CaseVariantCannotBypassManagedPluginBlockingViaLegacyDiscovery()
        => Assert.False(InstalledPluginGraph.ShouldDiscoverLegacyDirectory("plugin", ["Plugin"]));

    [Fact]
    public void DuplicateAndCaseCollidingIdsDoNotAbortUnrelatedStartup()
    {
        var result = InstalledPluginGraph.Resolve([P("A"), P("a"), P("B"), P("B"),
            P("Parent", new PackageDependency { Id = "A" }), P("Other")], _ => true);
        Assert.Equal(["Other"], result.Packages.Select(x => x.Id));
        Assert.Contains("A", result.Blocked.Keys);
        Assert.Contains("a", result.Blocked.Keys);
        Assert.Contains("B", result.Blocked.Keys);
        Assert.Contains("Parent", result.Blocked.Keys);
    }

    [Fact]
    public void UnsafeManagedIdNeverReachesDirectoryProbe()
    {
        var probed = new List<string>();
        var result = InstalledPluginGraph.Resolve([P("../escape"), P("Other")], id => { probed.Add(id); return true; });
        Assert.Equal(["Other"], probed);
        Assert.Contains("../escape", result.Blocked.Keys);
        Assert.True(InstalledPluginGraph.ShouldDiscoverLegacyDirectory("Legacy", []));
    }
}