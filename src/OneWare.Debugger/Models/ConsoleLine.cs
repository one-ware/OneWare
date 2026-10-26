namespace OneWare.Debugger.Models;

public enum ConsoleLineKind
{
    // Text from GDB or the program.
    Output,

    // A command the user typed into the console.
    Command,
    Error,
    Warning,

    // A note from the studio itself, e.g. "GDB exited."
    Info,

    // MI traffic the studio generates on its own: internal commands, prompts, result and async records.
    Protocol
}

// One line in the debugger console. Text: the text to display, without a line break.
public sealed record ConsoleLine(string Text, ConsoleLineKind Kind = ConsoleLineKind.Output)
{
    public bool IsCommand => Kind == ConsoleLineKind.Command;
    public bool IsError => Kind == ConsoleLineKind.Error;
    public bool IsWarning => Kind == ConsoleLineKind.Warning;
    public bool IsInfo => Kind == ConsoleLineKind.Info;
    public bool IsProtocol => Kind == ConsoleLineKind.Protocol;
    public bool IsMuted => Kind is ConsoleLineKind.Info or ConsoleLineKind.Protocol;
}
