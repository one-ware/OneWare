using Xunit;

namespace OneWare.Debugger.UnitTests;

/// <summary>
///     Deckt das Uebersetzen von GDB/MI-Records ab. Braucht keine GDB-Binary und kein Zielsystem,
///     laeuft also ueberall - was der Grund ist, warum ausgerechnet diese Schicht getestet wird:
///     sie ist die einzige, an der ein Fehler still zu falschen Anzeigewerten fuehrt statt zu
///     einer sichtbaren Fehlermeldung.
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

    [Fact]
    public void CommandResult_ReadsRunning()
    {
        Assert.Equal(CommandStatus.Running, new GdbCommandResult("^running").Status);
    }

    [Fact]
    public void CommandResult_ReadsConnected()
    {
        // Die Antwort auf -target-select. Wird sie nicht als Erfolg erkannt, bricht jede
        // Remote-Sitzung direkt nach dem Verbinden ab.
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
        // Der SVNR-Fall: kein Symbolfile, also weder file noch line. Nur der Programmzaehler
        // beschreibt den Halt, und der muss ankommen.
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

        // Typnamen mit Leerzeichen und eckigen Klammern duerfen den Tupel-Parser nicht aus dem
        // Tritt bringen.
        Assert.Equal("char [8]", locals.GetObject(1).GetValue("type"));
    }

    [Fact]
    public void CommandResult_ReadsEmptyLocals()
    {
        // Ohne Symbole meldet GDB eine leere Liste statt eines Fehlers. Das Panel muss dann leer
        // bleiben und darf nicht die Werte des vorigen Halts stehen lassen.
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
        // Eine nicht abgebildete Adresse ist der Normalfall beim Suchen, kein Ausnahmefall. Sie
        // muss als Fehler ankommen, damit die Zeile "unreadable" zeigt statt alte Bytes.
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

        // GDB laesst Luecken in der Nummerierung als leere Namen stehen. Die Anzeige filtert sie,
        // aber die Position der folgenden Register haengt daran, dass sie erhalten bleiben.
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

        // Die Nummer ist der Index in die Namensliste - sie ist nicht lueckenlos und darf deshalb
        // nicht durch die Position im Array ersetzt werden.
        Assert.Equal("2", values.GetObject(1).GetValue("number"));
        Assert.Equal("0x2001ffc0", values.GetObject(1).GetValue("value"));
    }
}
