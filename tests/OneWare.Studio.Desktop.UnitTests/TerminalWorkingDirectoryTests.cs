using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using OneWare.TerminalManager.ViewModels;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public sealed class TerminalWorkingDirectoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("oneware-terminal-workdir").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Fact]
    public void PrefixWorkingDirectory_QuotesThePathForEachShell()
    {
        Assert.Equal("cd -- '/a/it'\\''s dir' && ls",
            TerminalManagerViewModel.PrefixWorkingDirectory("ls", "/a/it's dir", false));
        Assert.Equal(@"Set-Location -LiteralPath 'C:\it''s dir' -ErrorAction Stop; dir",
            TerminalManagerViewModel.PrefixWorkingDirectory("dir", @"C:\it's dir", true));
    }

    [Theory]
    // A requested directory wins and persists; the shell only moves when it is elsewhere.
    [InlineData("/req", "/pool", "/shell", "/req")]
    [InlineData("/req", null, null, "/req")]
    [InlineData("/req", "/pool", "/req", null)]
    // Without a request the shell follows the pool, i.e. where the session's last command ended.
    [InlineData(null, "/pool", "/shell", "/pool")]
    [InlineData(null, "/pool", "/pool", null)]
    [InlineData(null, null, "/shell", null)]
    // A shell that does not report its directory is left alone unless a directory was requested.
    [InlineData(null, "/pool", null, null)]
    public void GetDirectoryChange_KeepsTheSessionDirectory(string? requested, string? pool, string? shell,
        string? expected)
    {
        Assert.Equal(expected, TerminalManagerViewModel.GetDirectoryChange(requested, pool, shell));
    }

    [Fact]
    public void PrefixWorkingDirectory_RunsTheCommandInTheDirectory_InBash()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var directory = Directory.CreateDirectory(Path.Combine(_root, "it's $HOME `x`")).FullName;
        File.WriteAllText(Path.Combine(directory, "marker.txt"), "found\n");

        // Starts elsewhere, like a reused shell after an earlier "cd".
        var command = TerminalManagerViewModel.PrefixWorkingDirectory("cat marker.txt\npwd", directory, false);
        var start = new ProcessStartInfo("bash", ["-c", command])
        {
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardOutput = true
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(["found", directory], output.TrimEnd().Split('\n'));
    }
}
