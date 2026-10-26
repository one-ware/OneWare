using OneWare.SoftwareProjectSystem.Services;
using OneWare.SoftwareProjectSystem.Templates;
using Xunit;

namespace OneWare.SoftwareProjectSystem.UnitTests;

public class SoftwareProjectFactoryTests
{
    private static SoftwareProjectFactory Create(params string[] available)
    {
        var kits = new ToolchainKitService(name => available.Contains(name) ? "/usr/bin/" + name : null);
        return new SoftwareProjectFactory(null!, kits, null!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("empty")]
    public void ResolveTemplate_EmptyMeansNoTemplate(string? name)
    {
        Assert.Null(SoftwareProjectFactory.ResolveTemplate(name));
    }

    [Fact]
    public void ResolveTemplate_IgnoresCase()
    {
        var expected = SoftwareProjectTemplates.All[0];
        Assert.Same(expected, SoftwareProjectFactory.ResolveTemplate(expected.Name.ToUpperInvariant()));
    }

    [Fact]
    public void ResolveTemplate_UnknownListsAvailable()
    {
        var e = Assert.Throws<SoftwareProjectCreationException>(() => SoftwareProjectFactory.ResolveTemplate("Rust"));
        Assert.Contains(SoftwareProjectTemplates.All[0].Name, e.Message);
    }

    [Fact]
    public void ResolveKit_ByIdOrName()
    {
        var factory = Create("gcc", "g++");

        Assert.Null(factory.ResolveKit(null));
        var kit = factory.ResolveKit("HOST-GCC");
        Assert.Equal("host-gcc", kit!.Id);
        Assert.Same(kit, factory.ResolveKit(kit.Name));
    }

    [Fact]
    public void ResolveKit_UnknownListsAvailable()
    {
        var e = Assert.Throws<SoftwareProjectCreationException>(() => Create("gcc", "g++").ResolveKit("msvc"));
        Assert.Contains("host-gcc", e.Message);
    }
}
