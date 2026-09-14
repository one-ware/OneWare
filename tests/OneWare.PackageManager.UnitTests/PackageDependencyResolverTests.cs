using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Services;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class PackageDependencyResolverTests
{
    private static PackageDependency Dep(string id, string? min = null, string? max = null) => new() { Id = id, MinVersion = min, MaxVersionExclusive = max };
    private static PackageVersion V(string version, params PackageDependency[] dependencies) => new() { Version = version, Dependencies = dependencies };
    private static Package P(string id, params PackageVersion[] versions) => new() { Id = id, Type = "Plugin", Name = id, Versions = versions };
    private static PackageOperationPlan Resolve(Package[] packages, InstalledPackage[]? installed = null, PackageRequest[]? roots = null,
        Func<Package, PackageVersion, bool>? supported = null) => new PackageDependencyResolver().Resolve(
            packages.ToDictionary(x => x.Id!), (installed ?? []).ToDictionary(x => x.Id), roots ?? [new("A")], supported ?? ((_, _) => true));
    private static InstalledPackage Installed(string id, string version, params PackageDependency[] deps) => new(id, "Plugin", id, null, null, null, version) { Dependencies = deps };

    [Fact]
    public void DiamondIsDeduplicatedAndDependencyFirst()
    {
        var plan = Resolve([P("A", V("1", Dep("B"), Dep("C"))), P("B", V("1", Dep("D"))), P("C", V("1", Dep("D"))), P("D", V("1"))]);
        Assert.True(plan.IsValid, string.Join("\n", plan.Errors));
        Assert.Equal(["D", "B", "C", "A"], plan.Items.Select(x => x.Id));
    }

    [Fact]
    public void ReusesInstalledVersionInsteadOfNewest()
    {
        var plan = Resolve([P("A", V("1", Dep("B", "1", "3"))), P("B", V("2"), V("1"))], [Installed("B", "1")]);
        Assert.Equal("1", plan.Items[0].Version);
        Assert.Equal(PackagePlanAction.Reuse, plan.Items[0].Action);
    }

    [Fact]
    public void UpgradesWhenInstalledVersionDoesNotSatisfyBounds()
    {
        var plan = Resolve([P("A", V("1", Dep("B", "2"))), P("B", V("1"), V("2"))], [Installed("B", "1")]);
        Assert.True(plan.IsValid);
        Assert.Equal(PackagePlanAction.Update, plan.Items[0].Action);
    }

    [Fact]
    public void BacktracksEarlierChoiceForSharedConstraint()
    {
        var plan = Resolve([P("A", V("1", Dep("B"), Dep("C"))), P("B", V("1"), V("2")), P("C", V("1", Dep("B", null, "2")))]);
        Assert.True(plan.IsValid, string.Join("\n", plan.Errors));
        Assert.Equal("1", plan.Items.Single(x => x.Id == "B").Version);
    }

    [Fact]
    public void FixedRootsAreNotSilentlyChanged()
    {
        var plan = Resolve([P("A", V("1", Dep("B", null, "2"))), P("B", V("1"), V("2"))], roots: [new("A", "1"), new("B", "2")]);
        Assert.False(plan.IsValid);
        Assert.Contains("conflict", plan.Errors[0]);
    }

    [Fact]
    public void OfflineReverseDependentBlocksUpgrade()
    {
        var plan = Resolve([P("A", V("1"), V("2"))], [Installed("A", "1"), Installed("OfflineParent", "1", Dep("A", null, "2"))], [new("A", "2")]);
        Assert.False(plan.IsValid);
        Assert.Contains("OfflineParent", plan.Errors[0]);
    }

    [Fact]
    public void JointUpdateCanReplaceReverseConstraint()
    {
        var plan = Resolve([P("A", V("1", Dep("B", null, "2")), V("2", Dep("B", "2"))), P("B", V("1"), V("2"))],
            [Installed("A", "1", Dep("B", null, "2")), Installed("B", "1")], [new("A", "2"), new("B", "2")]);
        Assert.True(plan.IsValid, string.Join("\n", plan.Errors));
        Assert.Equal(["B", "A"], plan.Items.Select(x => x.Id));
    }

    [Theory]
    [InlineData("1", "2", "1.0.0", true)]
    [InlineData("1", "2", "2.0.0", false)]
    [InlineData("1.2.3.4", null, "1.2.3.5", true)]
    [InlineData("1.0.0-beta", "1", "1.0.0-rc", true)]
    public void BoundsUseExistingVersionSemantics(string min, string? max, string version, bool expected)
        => Assert.Equal(expected, Dep("B", min, max).Accepts(version));

    [Theory]
    [InlineData("bad", null)]
    [InlineData("2", "1")]
    [InlineData("1", "1")]
    public void MalformedBoundsFailBeforeMutation(string min, string? max)
        => Assert.False(Resolve([P("A", V("1", Dep("B", min, max))), P("B", V("1"))]).IsValid);

    [Fact]
    public void CycleAndSelfAreRejected()
    {
        Assert.Contains("cycle", Resolve([P("A", V("1", Dep("A")))]).Errors[0]);
        Assert.Contains("cycle", Resolve([P("A", V("1", Dep("B"))), P("B", V("1", Dep("A")))]).Errors[0]);
    }

    [Fact]
    public void MissingOrNonPluginDependencyIsRejected()
    {
        Assert.False(Resolve([P("A", V("1", Dep("B")))]).IsValid);
        Assert.False(Resolve([P("A", V("1", Dep("B"))), new Package { Id = "B", Type = "NativeTool", Versions = [V("1")] }]).IsValid);
    }

    [Fact]
    public void SortsVersionsAndRejectsUnsupportedTargetsAndStudio()
    {
        Assert.Equal("10", Resolve([P("A", V("10"), V("2"))]).Items[0].Version);
        Assert.False(Resolve([P("A", V("1"))], supported: (_, _) => false).IsValid);
        Assert.False(Resolve([P("A", new PackageVersion { Version = "1", MinStudioVersion = "9999.0" })]).IsValid);
    }

    [Fact]
    public void NoImplicitPrereleaseOrDowngrade()
    {
        var package = P("A", V("1"), new PackageVersion { Version = "2-beta", IsPrerelease = true });
        Assert.Equal("1", Resolve([package]).Items[0].Version);
        Assert.Equal("2-beta", Resolve([package], roots: [new("A", IncludePrerelease: true)]).Items[0].Version);
        Assert.False(Resolve([P("A", V("1"))], [Installed("A", "2")]).IsValid);
    }

    [Fact]
    public void AmbiguousPackageIdsRejected() => Assert.False(Resolve([P("A", V("1")), P("a", V("1"))]).IsValid);

    [Fact]
    public void InstalledMetadataNotLatestMetadataDeterminesReuseEdges()
    {
        var plan = Resolve([P("A", V("1", Dep("B"))), P("B", V("1"), V("2", Dep("Missing")))], [Installed("B", "1")]);
        Assert.True(plan.IsValid);
        Assert.Empty(plan.Items[0].Dependencies);
    }

    [Theory]
    [InlineData("A.")]
    [InlineData(" A")]
    [InlineData("A ")]
    [InlineData("NUL")]
    [InlineData("con.dll")]
    [InlineData("COM1")]
    [InlineData("LPT9.log")]
    [InlineData("a\u0000b")]
    [InlineData("A*B")]
    [InlineData("A:B")]
    [InlineData("../B")]
    [InlineData("A.STAGE-deadbeef")]
    [InlineData("A.backup-deadbeef")]
    public void RejectsNonPortableAndReservedDirectoryIds(string id)
        => Assert.False(Resolve([P(id, V("1"))], roots: [new(id)]).IsValid);

    [Fact]
    public void CatalogKeyCannotHideUnsafeManifestIdentity()
    {
        var result = new PackageDependencyResolver().Resolve(
            new Dictionary<string, Package> { ["A"] = P("../escape", V("1")) },
            new Dictionary<string, InstalledPackage>(), [new("A")], (_, _) => true);
        Assert.False(result.IsValid);
        Assert.Contains("catalog key", result.Errors[0]);
    }

    [Fact]
    public void DuplicateDependencyIdsAreRejectedBeforeActivation()
        => Assert.False(Resolve([P("A", V("1", Dep("B"), Dep("B"))), P("B", V("1"))]).IsValid);

    [Fact]
    public void FingerprintIncludesResolvedActionsWhenInstallerSupportChanges()
    {
        var packages = new[] { P("A", V("1"), V("2")) };
        var before = Resolve(packages);
        var after = Resolve(packages, supported: (_, version) => version.Version == "1");
        Assert.True(before.IsValid);
        Assert.True(after.IsValid);
        Assert.NotEqual(before.Items[0].Version, after.Items[0].Version);
        Assert.NotEqual(before.Fingerprint, after.Fingerprint);
    }

    [Fact]
    public void FingerprintIncludesDownloadAndLicenseContentReferences()
    {
        Package Package(string url, string licenseUrl) => new()
        {
            Id = "A", Type = "Plugin", AcceptLicenseBeforeDownload = true,
            Tabs = [new PackageTab { Title = "License", ContentUrl = licenseUrl }],
            Versions = [new PackageVersion { Version = "1", Targets = [new PackageTarget { Target = "all", Url = url }] }]
        };
        var before = Resolve([Package("https://example.invalid/old", "https://example.invalid/license")]);
        Assert.NotEqual(before.Fingerprint,
            Resolve([Package("https://example.invalid/new", "https://example.invalid/license")]).Fingerprint);
        Assert.NotEqual(before.Fingerprint,
            Resolve([Package("https://example.invalid/old", "https://example.invalid/new-license")]).Fingerprint);
    }
}