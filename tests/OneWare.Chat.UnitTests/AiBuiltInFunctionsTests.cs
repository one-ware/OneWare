using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.AI;
using OneWare.Chat.Services;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class AiBuiltInFunctionsTests
{
    [Fact]
    public void PrepareTerminalCommand_LeavesSingleLinePowerShellUnchanged()
    {
        const string command = "Get-ChildItem | Select-Object -First 1";

        Assert.Equal(command, AiBuiltInFunctions.PrepareTerminalCommand(command, true));
    }

    [Fact]
    public void PrepareTerminalCommand_ClosesStandardInputOutsideWindows()
    {
        const string command = "for file in *; do\n  echo \"$file\"\ndone";

        Assert.Equal("{ " + command + "\n} < /dev/null", AiBuiltInFunctions.PrepareTerminalCommand(command, false));
    }

    [Fact]
    public void PrepareTerminalCommand_DoesNotWaitForInputAndKeepsShellState_InBash()
    {
        if (OperatingSystem.IsWindows()) return;

        var directory = System.IO.Directory.CreateTempSubdirectory("oneware-stdin").FullName;
        try
        {
            // cat would wait forever on the open stdin; cd/export must still affect the shell; a trailing
            // comment and background job must not break the wrapper.
            var prepared = AiBuiltInFunctions.PrepareTerminalCommand(
                $"cd '{directory}'; export OW_TEST=kept; cat; echo read-done # comment", false);
            var start = new System.Diagnostics.ProcessStartInfo("bash",
                ["-c", prepared + "\npwd; echo \"$OW_TEST\"; sleep 0 &"])
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            using var process = System.Diagnostics.Process.Start(start)!;
            Assert.True(process.WaitForExit(10_000), "the command waited for input");
            var output = process.StandardOutput.ReadToEnd();

            Assert.Equal(0, process.ExitCode);
            Assert.Equal(["read-done", directory, "kept"], output.TrimEnd().Split('\n'));
        }
        finally
        {
            System.IO.Directory.Delete(directory);
        }
    }

    [Fact]
    public void PrepareTerminalCommand_EncodesMultilinePowerShellAsSingleCommand()
    {
        const string command = "$items = @(1, 2)\r\nforeach ($item in $items) {\r\n  $item\r\n}";

        var prepared = AiBuiltInFunctions.PrepareTerminalCommand(command, true);

        Assert.DoesNotContain('\r', prepared);
        Assert.DoesNotContain('\n', prepared);

        const string prefix =
            "& ([ScriptBlock]::Create([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('";
        const string suffix = "'))))";
        Assert.StartsWith(prefix, prepared);
        Assert.EndsWith(suffix, prepared);

        var encoded = prepared[prefix.Length..^suffix.Length];
        Assert.Equal(command, Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
    }

    [Fact]
    public void GetTerminalSessionId_KeepsOneTerminalPoolPerChatSession()
    {
        Assert.Equal("AI Chat", AiBuiltInFunctions.GetTerminalSessionId(null));
        Assert.Equal("AI Chat:abc", AiBuiltInFunctions.GetTerminalSessionId("abc"));
    }

    [Fact]
    public void TryGetBackendContextValue_ReadsSessionAndToolCallIds()
    {
        var arguments = new AIFunctionArguments
        {
            Context = new Dictionary<object, object?>
            {
                ["invocation"] = new FakeInvocation { SessionId = "session-1", ToolCallId = "call-1" }
            }
        };

        Assert.Equal("session-1", AiFunctionProvider.TryGetBackendContextValue(arguments, "SessionId"));
        Assert.Equal("call-1", AiFunctionProvider.TryGetBackendContextValue(arguments, "ToolCallId"));
        Assert.Null(AiFunctionProvider.TryGetBackendContextValue(new AIFunctionArguments(), "SessionId"));
    }

    [Fact]
    public void TruncateTerminalOutput_CountsLinesTheTerminalAlreadyOmitted()
    {
        // 1000 captured lines out of 200000 printed; the terminal dropped the rest in the middle.
        var captured = string.Concat(Enumerable.Range(1, 500).Select(i => $"{i}\n")) +
                       "[... 199000 lines omitted ...]\n" +
                       string.Concat(Enumerable.Range(199501, 500).Select(i => $"{i}\n"));

        var truncated = AiBuiltInFunctions.TruncateTerminalOutput(captured, out var wasTruncated, 200000);

        Assert.True(wasTruncated);
        var lines = truncated.Split('\n');
        Assert.Equal("1", lines[0]);
        Assert.Equal("200000", lines[^2]);
        // 110 lines at the start and 109 at the end are shown.
        Assert.Contains(lines, l => l == "... [truncated 199781 lines] ...");
    }

    private sealed class FakeInvocation
    {
        public string SessionId { get; init; } = "";

        public string ToolCallId { get; init; } = "";
    }
}
