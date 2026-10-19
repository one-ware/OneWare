using System.Text.Json;
using OneWare.Copilot.Services;
using Xunit;

namespace OneWare.Copilot.UnitTests;

public class CopilotToolApprovalTests
{
    [Theory]
    [InlineData("ls -la", new[] { "ls" })]
    [InlineData("git status", new[] { "git status" })]
    [InlineData("git status && git diff --stat", new[] { "git status", "git diff" })]
    [InlineData("git -C repo status", new[] { "git" })]
    [InlineData("cd src; ls | grep foo", new[] { "cd", "ls", "grep" })]
    [InlineData("make 2>&1 | tail -n 20", new[] { "make", "tail" })]
    [InlineData("FOO=1 BAR=\"a b\" dotnet test -v minimal", new[] { "dotnet test" })]
    [InlineData("/usr/bin/python3 script.py", new[] { "python3" })]
    [InlineData("echo \"a; rm -rf /\"", new[] { "echo" })]
    [InlineData("Get-ChildItem -Recurse | Select-Object Name", new[] { "Get-ChildItem", "Select-Object" })]
    public void GetCommandPrefixes_ReturnsTheCommandsThatRun(string command, string[] expected)
    {
        Assert.Equal(expected, CopilotToolApproval.GetCommandPrefixes(command));
    }

    [Theory]
    [InlineData("echo $(rm -rf /)")]
    [InlineData("echo `whoami`")]
    [InlineData("sudo apt install foo")]
    [InlineData("bash -c 'rm -rf /'")]
    [InlineData("ls | xargs rm")]
    [InlineData("find . -name '*.o' -delete")]
    [InlineData("(cd src && make)")]
    [InlineData("ls\nrm -rf /")]
    [InlineData("echo \"unterminated")]
    [InlineData("   ")]
    public void GetCommandPrefixes_RefusesCommandsItCannotJudge(string command)
    {
        Assert.Null(CopilotToolApproval.GetCommandPrefixes(command));
    }

    [Fact]
    public void ForTool_ScopesTerminalApprovalsToTheCommands()
    {
        var args = JsonDocument.Parse("""{"command":"git status && ls"}""").RootElement;

        var scope = CopilotToolApproval.ForTool(CopilotToolApproval.TerminalToolName, "Execute In Terminal", args);

        Assert.NotNull(scope);
        Assert.Equal(["tool:runTerminalCommand:git status", "tool:runTerminalCommand:ls"], scope.Keys);
        Assert.Equal("Allow \"git status\", \"ls\" in this chat", scope.Label);
    }

    [Fact]
    public void ForTool_ReadsArgumentsEncodedAsString()
    {
        var args = JsonSerializer.SerializeToElement("""{"command":"ls"}""");

        var scope = CopilotToolApproval.ForTool(CopilotToolApproval.TerminalToolName, null, args);

        Assert.Equal(["tool:runTerminalCommand:ls"], scope?.Keys);
    }

    [Fact]
    public void ForTool_OffersNoChatApprovalForUnclearCommands()
    {
        var args = JsonDocument.Parse("""{"command":"sudo rm -rf /"}""").RootElement;

        Assert.Null(CopilotToolApproval.ForTool(CopilotToolApproval.TerminalToolName, null, args));
    }

    [Fact]
    public void ForTool_ApprovesOtherToolsAsAWhole()
    {
        var scope = CopilotToolApproval.ForTool("compileProject", "Compile Project", null);

        Assert.Equal(["tool:compileProject"], scope?.Keys);
        Assert.Equal("Allow Compile Project in this chat", scope?.Label);
    }
}
