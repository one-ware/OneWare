using System;
using System.Collections.Generic;
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

    [Fact]
    public void ReadRequiredAssemblies_ReadsNamesOfCompatibilityFile()
    {
        var names = PluginCompatibilityChecker.ReadRequiredAssemblies(
            "OneWare.Essentials : 1.2.3\r\nOneWare.AI.Extensibility : 4.5.6\n\ninvalid line\n");

        Assert.Equal(["OneWare.Essentials", "OneWare.AI.Extensibility"], names);
    }

    [Fact]
    public void CheckCompatibility_AcceptsAssemblyProvidedByLoadedPlugin()
    {
        const string deps = "Base.Extensibility : 1.4.2";

        Assert.False(PluginCompatibilityChecker.CheckCompatibility(deps).IsCompatible);

        var report = PluginCompatibilityChecker.CheckCompatibility(deps,
            new Dictionary<string, Version> { ["Base.Extensibility"] = new(1, 4, 5, 0) });

        Assert.True(report.IsCompatible);
    }

    [Theory]
    [InlineData("1.5.0.0")]
    [InlineData("1.4.1.0")]
    public void CheckCompatibility_RejectsIncompatiblePluginAssemblyVersion(string provided)
    {
        var report = PluginCompatibilityChecker.CheckCompatibility("Base.Extensibility : 1.4.2",
            new Dictionary<string, Version> { ["Base.Extensibility"] = Version.Parse(provided) });

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
