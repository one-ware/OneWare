namespace OneWare.Debugger.Models;

// Eine Zeile in einer der beiden Konsolen. IsCommand unterscheidet abgesetzte
// Kommandos von der Antwort des Backends, damit die Ansicht beide farblich trennen kann.
// Text: Der anzuzeigende Text ohne Zeilenumbruch.
// IsCommand: True, wenn die Zeile ein vom Studio abgesetztes Kommando ist.
public sealed record ConsoleLine(string Text, bool IsCommand = false);
