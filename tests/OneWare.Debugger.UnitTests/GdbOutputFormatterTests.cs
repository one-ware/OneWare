using OneWare.Debugger.Helpers;
using Xunit;

namespace OneWare.Debugger.UnitTests;

/// <summary>
///     Deckt ab, was in der Debugger Console landet. Der Formatter ist die einzige Stelle, an der
///     Protokollsyntax noch vor dem Nutzer abgefangen wird.
/// </summary>
public class GdbOutputFormatterTests
{
    [Theory]
    [InlineData("~\"Breakpoint 1 at 0x108\\n\"", "Breakpoint 1 at 0x108")]
    [InlineData("&\"warning: no symbols\\n\"", "warning: no symbols")]
    [InlineData("@\"hello from the target\\n\"", "hello from the target")]
    public void UnwrapsStreamRecords(string raw, string expected)
    {
        Assert.Equal(expected, GdbOutputFormatter.Format(raw));
    }

    [Fact]
    public void ReducesErrorToItsMessage()
    {
        Assert.Equal("error: No symbol \"foo\" in current context.",
            GdbOutputFormatter.Format("^error,msg=\"No symbol \\\"foo\\\" in current context.\""));
    }

    [Theory]
    [InlineData("^done")]
    [InlineData("=thread-group-added,id=\"i1\"")]
    [InlineData("   ")]
    public void DropsNoise(string raw)
    {
        Assert.Null(GdbOutputFormatter.Format(raw));
    }

    [Fact]
    public void KeepsPromptAndExecRecords()
    {
        Assert.Equal("(gdb)", GdbOutputFormatter.Format("(gdb)"));
        Assert.Equal("*stopped,reason=\"breakpoint-hit\"",
            GdbOutputFormatter.Format("*stopped,reason=\"breakpoint-hit\""));
    }
}
