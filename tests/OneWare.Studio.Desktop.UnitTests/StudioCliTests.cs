using OneWare.Studio.Desktop.Cli;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class StudioCliTests
{
    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "oneware://open?x=1" }, false)]
    [InlineData(new[] { "design.vhd" }, false)]
    [InlineData(new[] { "./project" }, false)]
    [InlineData(new[] { "--oneware-dir", "extensions" }, false)]
    [InlineData(new[] { "--autolaunch", "action" }, false)]
    [InlineData(new[] { "extensions", "list" }, true)]
    [InlineData(new[] { "--oneware-dir", "/tmp/x", "stop" }, true)]
    [InlineData(new[] { "-C", "/tmp", "cloud", "login" }, true)]
    [InlineData(new[] { "--help" }, true)]
    [InlineData(new[] { "design.vhd", "-h" }, true)]
    [InlineData(new[] { "--version" }, true)]
    public void IsCliInvocation_SeparatesCommandsFromGuiStartup(string[] args, bool expected)
    {
        Assert.Equal(expected, StudioCli.IsCliInvocation(args));
    }
}
