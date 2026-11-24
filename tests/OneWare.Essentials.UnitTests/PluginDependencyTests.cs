using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class PluginDependencyTests
{
    [Fact]
    public void Sort_LoadsPluginAfterThePluginItExtends()
    {
        var extension = new PluginLoadInfo("extension", ["OneWare.Essentials", "Base.Extensibility"], ["Extension"]);
        var other = new PluginLoadInfo("other", ["OneWare.Essentials"], ["Other"]);
        var basePlugin = new PluginLoadInfo("base", ["OneWare.Essentials"], ["Base", "Base.Extensibility"]);

        var order = PluginLoadOrder.Sort([extension, other, basePlugin]).Select(x => x.Path);

        Assert.Equal(["other", "base", "extension"], order);
    }

    [Fact]
    public void Sort_KeepsOrderOfIndependentPlugins()
    {
        var plugins = new[]
        {
            new PluginLoadInfo("b", ["OneWare.Essentials"], ["B"]),
            new PluginLoadInfo("a", ["OneWare.Essentials"], ["A"]),
            new PluginLoadInfo("c", [], ["C"])
        };

        Assert.Equal(["b", "a", "c"], PluginLoadOrder.Sort(plugins).Select(x => x.Path));
    }

    [Fact]
    public void Sort_IgnoresAssembliesThePluginShipsItself()
    {
        var first = new PluginLoadInfo("first", ["Shared"], ["First", "Shared"]);
        var second = new PluginLoadInfo("second", ["Shared"], ["Second", "Shared"]);

        Assert.Equal(["first", "second"], PluginLoadOrder.Sort([first, second]).Select(x => x.Path));
    }

    [Fact]
    public void Sort_AppendsPluginsInDependencyCycles()
    {
        var a = new PluginLoadInfo("a", ["B"], ["A"]);
        var b = new PluginLoadInfo("b", ["A"], ["B"]);
        var c = new PluginLoadInfo("c", [], ["C"]);

        Assert.Equal(["c", "a", "b"], PluginLoadOrder.Sort([a, b, c]).Select(x => x.Path));
    }

    private static readonly Dictionary<string, Version> NoAssemblies = new();

    private static PluginManifest Manifest(params (string Name, string Version)[] dependencies)
    {
        return new PluginManifest(1, dependencies.ToDictionary(x => x.Name, x => Version.Parse(x.Version)));
    }

    [Fact]
    public void Parse_ReadsDependencies()
    {
        var manifest = PluginManifest.Parse("""
            {
              // generated
              "manifestVersion": 1,
              "dependencies": {
                "OneWare.Essentials": "2027.1.0",
                "OneWare.AI.Extensibility": "4.5.6.0",
                "Some.Package": "1.2.0-beta.1",
              }
            }
            """);

        Assert.Equal(1, manifest.ManifestVersion);
        Assert.Equal(new Version(2027, 1, 0, 0), manifest.Dependencies["OneWare.Essentials"]);
        Assert.Equal(new Version(4, 5, 6, 0), manifest.Dependencies["oneware.ai.extensibility"]);
        Assert.Equal(new Version(1, 2, 0, 0), manifest.Dependencies["Some.Package"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("OneWare.Essentials : 1.0.0")]
    [InlineData("[]")]
    [InlineData("""{ "dependencies": {} }""")]
    [InlineData("""{ "manifestVersion": 1, "dependencies": [] }""")]
    [InlineData("""{ "manifestVersion": 1, "dependencies": { "OneWare.Essentials": "latest" } }""")]
    public void TryParse_RejectsInvalidManifest(string json)
    {
        Assert.False(PluginManifest.TryParse(json, out _));
        Assert.Contains(PluginCompatibilityChecker.CheckCompatibility(json, NoAssemblies).Issues,
            x => x.Kind == CompatibilityRecordKind.InvalidManifest);
    }

    [Fact]
    public void CheckCompatibilityPath_RejectsPluginWithoutManifest()
    {
        var path = Path.Combine(Path.GetTempPath(), "oneware-manifest-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            File.WriteAllText(Path.Combine(path, "compatibility.txt"), "OneWare.Essentials : 1.0.0");

            var report = PluginCompatibilityChecker.CheckCompatibilityPath(path, NoAssemblies);

            Assert.False(report.IsCompatible);
            Assert.Equal(CompatibilityRecordKind.InvalidManifest, Assert.Single(report.Issues).Kind);
            Assert.Contains(PluginManifest.FileName, report.Report);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void CheckCompatibility_RejectsNewerManifestVersion()
    {
        var manifest = new PluginManifest(PluginManifest.SupportedManifestVersion + 1, new Dictionary<string, Version>());

        var report = PluginCompatibilityChecker.CheckCompatibility(manifest, NoAssemblies, NoAssemblies);

        Assert.False(report.IsCompatible);
        Assert.True(report.StudioUpdateSuggested);
    }

    [Theory]
    [InlineData("2027.1.0", "2027.1.0.0", true)]
    [InlineData("2027.1.0", "2027.1.3.0", true)]
    [InlineData("2027.1.3", "2027.1.0.0", false)]
    [InlineData("2027.1.0", "2027.2.0.0", true)]
    [InlineData("2027.1.3", "2027.4.0.0", true)]
    [InlineData("2027.2.0", "2027.1.0.0", false)]
    [InlineData("2027.2.0", "2027.1.5.0", false)]
    [InlineData("2027.1.0", "2028.1.0.0", false)]
    [InlineData("2026.1.0", "2027.1.0.0", false)]
    public void CheckCompatibility_ChecksVersionOfOneWareAssemblies(string required, string provided, bool expected)
    {
        var report = PluginCompatibilityChecker.CheckCompatibility(Manifest(("OneWare.Essentials", required)),
            new Dictionary<string, Version> { ["OneWare.Essentials"] = Version.Parse(provided) }, NoAssemblies);

        Assert.Equal(expected, report.IsCompatible);
    }

    [Fact]
    public void CheckCompatibility_SuggestsStudioUpdateForNewerOneWareAssembly()
    {
        var report = PluginCompatibilityChecker.CheckCompatibility(Manifest(("OneWare.Essentials", "2027.2.0")),
            new Dictionary<string, Version> { ["OneWare.Essentials"] = new(2027, 1, 0, 0) }, NoAssemblies);

        Assert.True(report.StudioUpdateSuggested);
    }

    [Theory]
    [InlineData("4.4.1.0")]
    [InlineData("5.0.0.0")]
    [InlineData("3.0.0.0")]
    public void CheckCompatibility_OnlyRequiresThirdPartyAssembliesToBePresent(string provided)
    {
        var manifest = Manifest(("Jint", "4.4.1"));

        Assert.True(PluginCompatibilityChecker.CheckCompatibility(manifest,
            new Dictionary<string, Version> { ["Jint"] = Version.Parse(provided) }, NoAssemblies).IsCompatible);

        var missing = PluginCompatibilityChecker.CheckCompatibility(manifest, NoAssemblies, NoAssemblies);
        Assert.False(missing.IsCompatible);
        Assert.Equal(CompatibilityRecordKind.MissingDependency, Assert.Single(missing.Issues).Kind);
    }

    [Fact]
    public void CheckCompatibility_AcceptsAssemblyProvidedByLoadedPlugin()
    {
        var manifest = Manifest(("Base.Extensibility", "1.4.2"));

        Assert.False(PluginCompatibilityChecker.CheckCompatibility(manifest, NoAssemblies, NoAssemblies).IsCompatible);

        var report = PluginCompatibilityChecker.CheckCompatibility(manifest, NoAssemblies,
            new Dictionary<string, Version> { ["Base.Extensibility"] = new(1, 4, 5, 0) });

        Assert.True(report.IsCompatible);
    }

    [Fact]
    public void CheckCompatibility_AcceptsNewerMinorOfPluginAssembly()
    {
        var report = PluginCompatibilityChecker.CheckCompatibility(Manifest(("Base.Extensibility", "1.4.2")),
            NoAssemblies, new Dictionary<string, Version> { ["Base.Extensibility"] = new(1, 5, 0, 0) });

        Assert.True(report.IsCompatible);
    }

    [Theory]
    [InlineData("2.0.0.0")]
    [InlineData("1.4.1.0")]
    [InlineData("1.3.9.0")]
    public void CheckCompatibility_RejectsIncompatiblePluginAssemblyVersion(string provided)
    {
        var report = PluginCompatibilityChecker.CheckCompatibility(Manifest(("Base.Extensibility", "1.4.2")),
            NoAssemblies, new Dictionary<string, Version> { ["Base.Extensibility"] = Version.Parse(provided) });

        Assert.False(report.IsCompatible);
    }

    [Theory]
    [InlineData("1.2.0", "1.2.0", true)]
    [InlineData("1.2.0", "1.3.0-beta.1", true)]
    [InlineData("1.2.0", "1.1.9", false)]
    [InlineData("1.2.0", null, false)]
    [InlineData(null, "0.0.1", true)]
    [InlineData("1.2.0", "local-build", true)]
    public void IsSatisfiedBy_ComparesWithMinVersion(string? minVersion, string? version, bool expected)
    {
        var dependency = new PackageDependency { Id = "base", MinVersion = minVersion };

        Assert.Equal(expected, dependency.IsSatisfiedBy(version));
    }
}
