using System.Text;

namespace OneWare.Debugger.Helpers;

/// <summary>
///     Bereitet rohe GDB/MI-Zeilen fuer die Anzeige auf.
///     GDB/MI kennt drei Stream-Records, deren Nutzlast ein C-escapter String ist:
///     <c>~</c> Konsole, <c>@</c> Zielprogramm, <c>&amp;</c> Log. Ihre Anfuehrungszeichen und
///     Escape-Sequenzen sind Protokoll und gehoeren nicht auf den Bildschirm.
///     Exec-Async-Records (<c>*</c>) bleiben unveraendert stehen, weil sie im Reiter
///     "Debugger Console" genau das sind, was man sehen will. Ebenso die Eingabeaufforderung
///     <c>(gdb)</c>, die die Antworten optisch trennt. Entfernt werden die inhaltsleere Quittung
///     <c>^done</c> und die Notify-Records (<c>=</c>); <c>^error</c> wird auf seine Meldung
///     eingedampft.
/// </summary>
public static class GdbOutputFormatter
{
    /// <summary>
    ///     Liefert die anzuzeigende Zeile oder <c>null</c>, wenn die Zeile reines Rauschen ist.
    /// </summary>
    public static string? Format(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return null;

        var line = rawLine.Trim();

        // Die Eingabeaufforderung bleibt stehen: Sie trennt die Antworten optisch voneinander
        // und macht sichtbar, dass GDB wieder bereit ist.
        if (line == "(gdb)") return line;

        // Die blosse Erfolgsmeldung eines Kommandos ist Protokoll-Quittung ohne Inhalt.
        if (line == "^done") return null;

        // Notify-Records melden interne Buchfuehrung von GDB - Thread-Gruppen, geladene
        // Bibliotheken und Aehnliches. Fuer den Benutzer ist davon nichts brauchbar.
        if (line[0] == '=') return null;

        // Fehler tragen ihre Meldung als C-escapten String mit; nur der interessiert.
        if (line.StartsWith("^error,msg=", StringComparison.Ordinal))
            return $"error: {Unquote(line["^error,msg=".Length..])}";

        if (line.Length < 2 || line[0] is not ('~' or '@' or '&')) return line;

        var payload = Unquote(line[1..]);

        // Stream-Records enden fast immer auf einem Zeilenumbruch, den die Ansicht selbst setzt.
        return payload.TrimEnd('\r', '\n') is { Length: > 0 } trimmed ? trimmed : null;
    }

    /// <summary>
    ///     Entfernt die umschliessenden Anfuehrungszeichen und loest die C-Escape-Sequenzen auf.
    /// </summary>
    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];

        var result = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                result.Append(value[i]);
                continue;
            }

            i++;
            result.Append(value[i] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '"' => '"',
                '\\' => '\\',
                _ => value[i]
            });
        }

        return result.ToString();
    }
}
