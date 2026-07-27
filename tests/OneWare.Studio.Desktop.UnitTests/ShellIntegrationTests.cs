using System;
using System.IO;
using OneWare.Terminal;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class ShellIntegrationTests
{
    [Fact]
    public void GetSpawnConfig_Bash_UsesInitFileWithIntegrationScript()
    {
        var config = ShellIntegration.GetSpawnConfig("/bin/bash");

        Assert.NotNull(config.Arguments);
        Assert.StartsWith("--init-file ", config.Arguments);

        var scriptPath = config.Arguments!["--init-file ".Length..];
        Assert.True(File.Exists(scriptPath));

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("633;C", script);
        Assert.Contains("633;D", script);
        Assert.Contains("$HOME/.bashrc", script);
    }

    [Fact]
    public void GetSpawnConfig_Zsh_RedirectsZdotdir()
    {
        var config = ShellIntegration.GetSpawnConfig("/usr/bin/zsh");

        Assert.Equal("-i", config.Arguments);
        Assert.NotNull(config.Environment);
        Assert.Contains("ZDOTDIR=", config.Environment);
        Assert.Contains("OW_USER_ZDOTDIR=", config.Environment);

        var zdotdir = config.Environment!.Split('\0')[0]["ZDOTDIR=".Length..];
        Assert.True(File.Exists(Path.Combine(zdotdir, ".zshrc")));
        Assert.True(File.Exists(Path.Combine(zdotdir, ".zshenv")));
    }

    [Fact]
    public void GetSpawnConfig_PowerShell_DotSourcesIntegrationScript()
    {
        var config = ShellIntegration.GetSpawnConfig("powershell.exe");

        Assert.NotNull(config.Arguments);
        Assert.StartsWith("powershell.exe ", config.Arguments);
        Assert.Contains("-NoExit", config.Arguments);
        Assert.Contains("integration.ps1", config.Arguments);
    }

    [Fact]
    public void GetSpawnConfig_UnknownShell_ReturnsNoIntegration()
    {
        var config = ShellIntegration.GetSpawnConfig("/usr/bin/fish");

        Assert.Null(config.Arguments);
        Assert.Null(config.Environment);
    }
}
