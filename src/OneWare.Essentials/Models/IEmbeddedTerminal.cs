namespace OneWare.Essentials.Models;

/// <summary>
/// A terminal that is rendered by an arbitrary host — e.g. the mini terminal inside an AI chat
/// tool box — instead of the terminal pane. Bind the instance to a <c>ContentControl</c> to
/// display it; it brings its own header and the action that moves it into the terminal pane.
/// A terminal can only be rendered in one place at a time, because every terminal control
/// consumes the shell's byte stream and negotiates the pty window size on its own.
/// </summary>
public interface IEmbeddedTerminal
{
    /// <summary>The command line that runs in this terminal.</summary>
    string Command { get; }

    /// <summary>True once the terminal has been moved into the terminal pane.</summary>
    bool IsShownInPane { get; }

    /// <summary>
    /// Terminates the shell. Output that has already been rendered stays visible, so this is
    /// the way to release the shell process once a command is done.
    /// </summary>
    void CloseShell();
}
