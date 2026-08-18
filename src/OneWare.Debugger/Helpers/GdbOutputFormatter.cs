using System.Text;

namespace OneWare.Debugger.Helpers;

// Bereitet rohe GDB/MI-Zeilen fuer die Anzeige auf.
// GDB/MI kennt drei Stream-Records, deren Nutzlast ein C-escapter String ist:
// ~ Konsole, @ Zielprogramm, & Log. Ihre Anfuehrungszeichen und
// Escape-Sequenzen sind Protokoll und gehoeren nicht auf den Bildschirm.
// Exec-Async-Records (*) bleiben unveraendert stehen, weil sie im Reiter
// "Debugger Console" genau das sind, was man sehen will. Ebenso die Eingabeaufforderung
// (gdb), die die Antworten optisch trennt. Entfernt werden die inhaltsleere Quittung
// ^done und die Notify-Records (=); ^error wird auf seine Meldung
// eingedampft.
public static class GdbOutputFormatter
{
    // Liefert die anzuzeigende Zeile oder null, wenn die Zeile reines Rauschen ist.
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

    // Entfernt die umschliessenden Anfuehrungszeichen und loest die C-Escape-Sequenzen auf.
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
