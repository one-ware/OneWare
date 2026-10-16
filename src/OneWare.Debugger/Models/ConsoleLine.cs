namespace OneWare.Debugger.Models;

// One line in one of the two consoles. IsCommand distinguishes issued
// commands from the backend's answer, so that the view can colour the two differently.
// Text: the text to display, without a line break.
// IsCommand: true if the line is a command issued by the studio.
public sealed record ConsoleLine(string Text, bool IsCommand = false);
