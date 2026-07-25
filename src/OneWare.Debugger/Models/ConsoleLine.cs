namespace OneWare.Debugger.Models;

/// <summary>
///     Eine Zeile in einer der beiden Konsolen. <see cref="IsCommand" /> unterscheidet abgesetzte
///     Kommandos von der Antwort des Backends, damit die Ansicht beide farblich trennen kann.
/// </summary>
/// <param name="Text">Der anzuzeigende Text ohne Zeilenumbruch.</param>
/// <param name="IsCommand">True, wenn die Zeile ein vom Studio abgesetztes Kommando ist.</param>
public sealed record ConsoleLine(string Text, bool IsCommand = false);
