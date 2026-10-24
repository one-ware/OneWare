using Xunit;

namespace OneWare.Debugger.UnitTests;

/// <summary>
///     Covers translating GDB/MI records. Needs no GDB binary and no target system,
///     so it runs everywhere - which is the reason why this layer in particular is tested:
///     it is the only one where a bug silently leads to wrong display values instead of
///     a visible error message.
/// </summary>
public class GdbOutputParsingTests
{
    [Fact]
    public void CommandResult_ReadsDoneWithResults()
    {
        var result = new GdbCommandResult("^done,bkpt={number=\"1\",file=\"main.c\",line=\"42\"}");

        Assert.Equal(CommandStatus.Done, result.Status);

        var breakpoint = result.GetObject("bkpt");
        Assert.Equal("1", breakpoint.GetValue("number"));
        Assert.Equal("main.c", breakpoint.GetValue("file"));
        Assert.Equal("42", breakpoint.GetValue("line"));
    }

    [Fact]
    public void CommandResult_ReadsErrorMessage()
    {
        var result = new GdbCommandResult("^error,msg=\"No symbol foo in current context.\"");

        Assert.Equal(CommandStatus.Error, result.Status);
        Assert.Equal("No symbol foo in current context.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(@"C:\\Projects\\Demo\\asm\\wave.asm", @"C:\Projects\Demo\asm\wave.asm")]
    [InlineData("/Projects/Demo/asm/wave.asm", "/Projects/Demo/asm/wave.asm")]
    [InlineData("/home/user/Projects/Demo/asm/wave.asm", "/home/user/Projects/Demo/asm/wave.asm")]
    public void CommandResult_ReadsSourcePathsOnEveryPlatform(string escaped, string expected)
    {
        var result = new GdbCommandResult(
            $"^done,files=[{{file=\"asm/wave.asm\",fullname=\"{escaped}\",debug-fully-read=\"false\"}}]");

        Assert.Equal(expected, result.GetObject("files").GetObject(0).GetValue("fullname"));
    }

    [Fact]
    public void CommandResult_UnescapesQuotesInMessages()
    {
        var result = new GdbCommandResult("""^error,msg="No symbol \"foo\" in current context.\n" """.TrimEnd());

        Assert.Equal("No symbol \"foo\" in current context.\n", result.ErrorMessage);
    }

    [Fact]
    public void CommandResult_ReadsRunning()
    {
        Assert.Equal(CommandStatus.Running, new GdbCommandResult("^running").Status);
    }

    [Fact]
    public void CommandResult_ReadsConnected()
    {
        // The answer to -target-select. If it is not recognised as success, every
        // remote session aborts right after connecting.
        Assert.Equal(CommandStatus.Connected, new GdbCommandResult("^connected").Status);
    }

    [Fact]
    public void Event_ReadsBreakpointStop()
    {
        var gdbEvent = new GdbEvent(
            "*stopped,reason=\"breakpoint-hit\",disp=\"keep\",bkptno=\"1\"," +
            "frame={addr=\"0x00400500\",func=\"main\",file=\"main.c\",fullname=\"/src/main.c\",line=\"7\"}");

        Assert.Equal("stopped", gdbEvent.Name);
        Assert.Equal("breakpoint-hit", gdbEvent.Reason);

        var frame = gdbEvent.GetObject("frame");
        Assert.Equal("main", frame.GetValue("func"));
        Assert.Equal("/src/main.c", frame.GetValue("fullname"));
        Assert.Equal("7", frame.GetValue("line"));
    }

    [Fact]
    public void Event_ReadsRunning()
    {
        var gdbEvent = new GdbEvent("*running,thread-id=\"all\"");

        Assert.Equal("running", gdbEvent.Name);
        Assert.Equal("all", gdbEvent.GetValue("thread-id"));
    }

    [Fact]
    public void Event_ReadsStopWithoutSourceLocation()
    {
        // A target without a symbol file: neither file nor line. Only the program counter
        // describes the halt, and it must arrive.
        var gdbEvent = new GdbEvent(
            "*stopped,reason=\"end-stepping-range\",frame={addr=\"0x00000108\"}");

        var frame = gdbEvent.GetObject("frame");
        Assert.Equal("0x00000108", frame.GetValue("addr"));
        Assert.Equal(string.Empty, frame.GetValue("fullname"));
        Assert.Equal(string.Empty, frame.GetValue("line"));
    }

    [Fact]
    public void CommandResult_ReadsLocals()
    {
        var result = new GdbCommandResult(
            "^done,locals=[{name=\"i\",type=\"int\",value=\"3\"}," +
            "{name=\"buf\",type=\"char [8]\",value=\"0x2001ff80\"}]");

        var locals = result.GetObject("locals");

        Assert.Equal(2, locals.Count);
        Assert.Equal("i", locals.GetObject(0).GetValue("name"));
        Assert.Equal("int", locals.GetObject(0).GetValue("type"));
        Assert.Equal("3", locals.GetObject(0).GetValue("value"));

        // Type names with spaces and square brackets must not throw the tuple parser
        // off.
        Assert.Equal("char [8]", locals.GetObject(1).GetValue("type"));
    }

    [Fact]
    public void CommandResult_ReadsEmptyLocals()
    {
        // Without symbols GDB reports an empty list instead of an error. The panel must then stay
        // empty and must not keep the values of the previous halt.
        var result = new GdbCommandResult("^done,locals=[]");

        Assert.Equal(CommandStatus.Done, result.Status);
        Assert.Equal(0, result.GetObject("locals").Count);
    }

    [Fact]
    public void CommandResult_ReadsMemoryBytes()
    {
        var result = new GdbCommandResult(
            "^done,memory=[{begin=\"0x2001ff80\",offset=\"0x00000000\",end=\"0x2001ff84\"," +
            "contents=\"0011aabb\"}]");

        var blocks = result.GetObject("memory");

        Assert.Equal(1, blocks.Count);
        Assert.Equal("0x2001ff80", blocks.GetObject(0).GetValue("begin"));
        Assert.Equal("0011aabb", blocks.GetObject(0).GetValue("contents"));
    }

    [Fact]
    public void CommandResult_ReportsUnreadableMemoryAsError()
    {
        // An unmapped address is the normal case when searching, not an exception. It
        // must arrive as an error so that the row shows "unreadable" instead of old bytes.
        var result = new GdbCommandResult("^error,msg=\"Cannot access memory at address 0x0\"");

        Assert.Equal(CommandStatus.Error, result.Status);
        Assert.Equal("Cannot access memory at address 0x0", result.ErrorMessage);
    }

    [Fact]
    public void CommandResult_ReadsRegisterNames()
    {
        var result = new GdbCommandResult("^done,register-names=[\"zero\",\"ra\",\"sp\",\"\",\"pc\"]");

        var names = result.GetObject("register-names");

        Assert.Equal(5, names.Count);
        Assert.Equal("zero", names.GetValue(0));
        Assert.Equal("sp", names.GetValue(2));

        // GDB leaves gaps in the numbering as empty names. The display filters them,
        // but the position of the following registers depends on them being preserved.
        Assert.Equal(string.Empty, names.GetValue(3));
        Assert.Equal("pc", names.GetValue(4));
    }

    [Fact]
    public void CommandResult_ReadsRegisterValues()
    {
        var result = new GdbCommandResult(
            "^done,register-values=[{number=\"0\",value=\"0x0\"},{number=\"2\",value=\"0x2001ffc0\"}]");

        var values = result.GetObject("register-values");

        Assert.Equal(2, values.Count);
        Assert.Equal("0", values.GetObject(0).GetValue("number"));
        Assert.Equal("0x0", values.GetObject(0).GetValue("value"));

        // The number is the index into the name list - it is not gapless and therefore must
        // not be replaced by the position in the array.
        Assert.Equal("2", values.GetObject(1).GetValue("number"));
        Assert.Equal("0x2001ffc0", values.GetObject(1).GetValue("value"));
    }
}
