using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// Eine Zeile im Memory-Reiter: eine vom Benutzer eingetragene Adresse und die zuletzt
// dort gelesenen Bytes.
// Die Tabelle zeigt nur an; Zeilen entstehen und verschwinden ueber die Knoepfe der Leiste.
public partial class MemoryRow : ObservableObject
{
    // Adresse oder Ausdruck in der Schreibweise, die das Backend versteht - eine Zahl, oder ein
    // Symbol wie &buffer. Gezaehlt wird in adressierbaren Einheiten des Ziels, nicht in Bytes;
    // die Umrechnung macht der Reiter anhand des Profils.
    [ObservableProperty] private string _address = string.Empty;

    // Anzahl der zu lesenden adressierbaren Einheiten - Bytes auf einer byteadressierten
    // Maschine, sonst deren Wortbreite. Eine neue Zeile liest zunaechst eine Einheit.
    [ObservableProperty] private int _length = 1;

    // Wie viele Bytes die Zeile umfasst - Length mal der Breite einer Einheit. Nur zur Anzeige;
    // gelesen wird weiterhin ueber Length.
    [ObservableProperty] private int _bytes = 1;

    // Die gelesenen Bytes, oder ein Hinweis, warum nichts gelesen werden konnte.
    [ObservableProperty] private string _value = string.Empty;

    // Dasselbe wie Value, aber in der Form, in der es vom Backend kam: Einheiten in Hex.
    // Bleibt stehen, damit ein Wechsel des Zahlensystems die Zeile nur neu beschriftet, statt
    // dafuer das Ziel noch einmal lesen zu muessen -> das ginge waehrend eines Laufs gar nicht.
    public string Raw { get; set; } = string.Empty;
}
