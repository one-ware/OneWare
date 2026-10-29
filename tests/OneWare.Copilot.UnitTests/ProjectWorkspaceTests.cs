using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneWare.Copilot.Services;
using Xunit;

namespace OneWare.Copilot.UnitTests;

public sealed class ProjectWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OneWare-Workspace-" + Guid.NewGuid());

    public ProjectWorkspaceTests()
    {
        Directory.CreateDirectory(_root);
    }

    private string Project(string name)
    {
        return Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void LoginShellEnvironment_ParsesOutputAfterProfileBanners()
    {
        var env = LoginShellEnvironment.Parse(
            "Welcome!\n__ONEWARE_SHELL_ENV__TOKEN=a=b\0MULTI=line1\nline2\0\0BROKEN\0");

        Assert.Equal("a=b", env["TOKEN"]);
        Assert.Equal("line1\nline2", env["MULTI"]);
        Assert.Equal(2, env.Count);
        Assert.Empty(LoginShellEnvironment.Parse("no marker"));
    }

    [Fact]
    public void LoginShellEnvironment_ResolvesBareOrMissingShell()
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/bin/sh")) return;
        var directory = Path.GetDirectoryName(Path.GetFullPath("/bin/sh"))!;

        Assert.Equal("/bin/sh", LoginShellEnvironment.ResolveShell("/bin/sh", null, () => null));
        // Bare names, as set by some launchers, are looked up in PATH.
        Assert.Equal(Path.Combine(directory, "sh"), LoginShellEnvironment.ResolveShell("sh", directory, () => null));
        // Otherwise the account's shell is used.
        Assert.Equal("/bin/sh", LoginShellEnvironment.ResolveShell(null, null, () => "/bin/sh"));
        Assert.Equal("/bin/sh", LoginShellEnvironment.ResolveShell("no-such-shell", "", () => "/missing"));
    }

    [Fact]
    public void GetProjectSkillDirectories_SkipsTheWorkingDirectoryProject()
    {
        var active = Project("active");
        var other = Project("other");
        Directory.CreateDirectory(Path.Combine(active, ".github", "skills"));
        Directory.CreateDirectory(Path.Combine(other, ".github", "skills"));
        Directory.CreateDirectory(Path.Combine(other, ".agents", "skills"));

        var directories = CopilotSessionConfiguration.GetProjectSkillDirectories([active, other], active).ToList();

        Assert.Equal([Path.Combine(other, ".github", "skills"), Path.Combine(other, ".agents", "skills")],
            directories);
    }

    [Fact]
    public void BuildWorkspaceContext_InlinesInstructionsOfOtherProjects()
    {
        var active = Project("active");
        var other = Project("other");
        Write(Path.Combine(active, "AGENTS.md"), "active rules");
        Write(Path.Combine(other, "AGENTS.md"), "other rules");
        Write(Path.Combine(other, ".github", "copilot-instructions.md"), "copilot rules");

        var text = CopilotSessionConfiguration.BuildWorkspaceContext([active, other], active, out var key);

        Assert.NotNull(text);
        Assert.Contains($"- active: {active} (working directory)", text);
        Assert.Contains($"- other: {other}", text);
        Assert.Contains("other rules", text);
        Assert.Contains("copilot rules", text);
        Assert.DoesNotContain("active rules", text);

        // The key only changes with what the session would be told.
        CopilotSessionConfiguration.BuildWorkspaceContext([active, other], active, out var unchanged);
        Assert.Equal(key, unchanged);
        Write(Path.Combine(other, "AGENTS.md"), "other rules, updated");
        CopilotSessionConfiguration.BuildWorkspaceContext([active, other], active, out var changed);
        Assert.NotEqual(key, changed);
    }

    [Fact]
    public void BuildWorkspaceContext_IsEmptyWhenOnlyTheWorkingDirectoryIsOpen()
    {
        var active = Project("active");
        Write(Path.Combine(active, "AGENTS.md"), "rules");

        Assert.Null(CopilotSessionConfiguration.BuildWorkspaceContext([active], active, out _));
    }

    [Fact]
    public void BuildWorkspaceContext_TruncatesLargeInstructionFiles()
    {
        var active = Project("active");
        var other = Project("other");
        Write(Path.Combine(other, "AGENTS.md"), new string('x', CopilotSessionConfiguration.MaxInstructionFileChars + 10));

        var text = CopilotSessionConfiguration.BuildWorkspaceContext([active, other], active, out _);

        Assert.Contains("[truncated, read", text);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }
}
