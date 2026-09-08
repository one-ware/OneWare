using System;
using System.Text;
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
    public void PrepareTerminalCommand_LeavesMultilineCommandsUnchangedOutsideWindows()
    {
        const string command = "for file in *; do\n  echo \"$file\"\ndone";

        Assert.Equal(command, AiBuiltInFunctions.PrepareTerminalCommand(command, false));
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
}
