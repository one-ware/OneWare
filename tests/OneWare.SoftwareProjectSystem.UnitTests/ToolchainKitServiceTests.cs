using OneWare.SoftwareProjectSystem.Services;
using Xunit;

namespace OneWare.SoftwareProjectSystem.UnitTests;

public class ToolchainKitServiceTests
{
    private static ToolchainKitService Create(params string[] available)
    {
        return new ToolchainKitService(name => available.Contains(name) ? "/usr/bin/" + name : null);
    }

    [Fact]
    public void DetectsHostAndCrossKits()
    {
        var service = Create("gcc", "g++", "gdb", "arm-none-eabi-gcc", "arm-none-eabi-g++", "arm-none-eabi-gdb");

        var kits = service.Kits;

        var host = Assert.Single(kits, x => x.Id == "host-gcc");
        Assert.True(host.IsHost);
        Assert.Equal("/usr/bin/gdb", host.Gdb);

        var arm = Assert.Single(kits, x => x.Id == "arm-none-eabi");
        Assert.False(arm.IsHost);
        Assert.Equal(".elf", arm.ExecutableExtension);
        Assert.Equal("/usr/bin/arm-none-eabi-gdb", arm.Gdb);
    }

    [Fact]
    public void NeedsBothCompilers()
    {
        var service = Create("gcc", "clang++");
        Assert.Empty(service.Kits);
    }

    [Fact]
    public void RegisteredKitsWinOverDetectedOnes()
    {
        var service = Create("gcc", "g++");
        service.RegisterKit(new ToolchainKit("host-gcc", "Custom", "/opt/gcc", "/opt/g++"));

        var kit = Assert.Single(service.Kits);
        Assert.Equal("Custom", kit.Name);
        Assert.Equal(kit, service.GetKit("host-gcc"));
    }
}
