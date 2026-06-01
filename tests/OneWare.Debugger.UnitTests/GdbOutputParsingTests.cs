using Xunit;

namespace OneWare.Debugger.UnitTests;
public class GdbOutputParsingTests
{
    [Fact]
    public void GdbCommandResult_Parses_DoneWithResults()
    {
        var result = new GdbCommandResult("^done,bkpt={number=\"1\",file=\"main.c\",line=\"42\"}");

        Assert.Equal(CommandStatus.Done, result.Status);
        var bkpt = result.GetObject("bkpt");
        Assert.Equal("1", bkpt.GetValue("number"));
        Assert.Equal("main.c", bkpt.GetValue("file"));
        Assert.Equal("42", bkpt.GetValue("line"));
    }

    [Fact]
    public void GdbCommandResult_Parses_Error()
    {
        var result = new GdbCommandResult("^error,msg=\"No symbol foo in current context.\"");

        Assert.Equal(CommandStatus.Error, result.Status);
        Assert.Equal("No symbol foo in current context.", result.ErrorMessage);
    }

    [Fact]
    public void GdbCommandResult_Parses_Running()
    {
        var result = new GdbCommandResult("^running");
        Assert.Equal(CommandStatus.Running, result.Status);
    }

    [Fact]
    public void GdbCommandResult_Parses_Connected()
    {
        var result = new GdbCommandResult("^connected");
        Assert.Equal(CommandStatus.Connected, result.Status);
    }

    [Fact]
    public void GdbEvent_Parses_StoppedAtBreakpoint()
    {
        var ev = new GdbEvent(
            "*stopped,reason=\"breakpoint-hit\",disp=\"keep\",bkptno=\"1\"," +
            "frame={addr=\"0x00400500\",func=\"main\",file=\"main.c\",fullname=\"/src/main.c\",line=\"7\"}");

        Assert.Equal("stopped", ev.Name);
        Assert.Equal("breakpoint-hit", ev.Reason);

        var frame = ev.GetObject("frame");
        Assert.Equal("main", frame.GetValue("func"));
        Assert.Equal("/src/main.c", frame.GetValue("fullname"));
        Assert.Equal("7", frame.GetValue("line"));
    }

    [Fact]
    public void GdbEvent_Parses_RunningEvent()
    {
        var ev = new GdbEvent("*running,thread-id=\"all\"");
        Assert.Equal("running", ev.Name);
        Assert.Equal("all", ev.GetValue("thread-id"));
    }
}
