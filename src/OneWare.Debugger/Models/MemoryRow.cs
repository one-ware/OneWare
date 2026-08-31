using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// Eine Zeile im Memory-Reiter: eine vom Benutzer eingetragene Adresse und die zuletzt
// dort gelesenen Bytes.
// Address und Length sind bearbeitbar, weil man beim Suchen
// eines Speicherbereichs die Adresse laufend anpasst. Beide loesen ein erneutes Lesen aus,
// sobald sie sich aendern.
public partial class MemoryRow : ObservableObject
{
    // Adresse oder Ausdruck in der Schreibweise, die das Backend versteht - eine Zahl, oder ein
    // Symbol wie &buffer. Gezaehlt wird in adressierbaren Einheiten des Ziels, nicht in Bytes;
    // die Umrechnung macht der Reiter anhand des Profils.
    [ObservableProperty] private string _address = string.Empty;

    // Anzahl der zu lesenden adressierbaren Einheiten - Bytes auf einer byteadressierten
    // Maschine, sonst deren Wortbreite. Den Anfangswert einer neuen Zeile setzt der Reiter aus
    // dem Profil des Ziels; die Vier hier gilt nur, wenn keines vorliegt.
    [ObservableProperty] private int _length = 4;

    // Die gelesenen Bytes, oder ein Hinweis, warum nichts gelesen werden konnte.
    [ObservableProperty] private string _value = string.Empty;
}
