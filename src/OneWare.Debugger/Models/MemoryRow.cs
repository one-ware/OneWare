using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// Eine Zeile im Memory-Reiter: eine vom Benutzer eingetragene Adresse und die zuletzt
// dort gelesenen Bytes.
// Address und Length sind bearbeitbar, weil man beim Suchen
// eines Speicherbereichs die Adresse laufend anpasst. Beide loesen ein erneutes Lesen aus,
// sobald sie sich aendern.
public partial class MemoryRow : ObservableObject
{
    // Adresse oder Ausdruck, in der Schreibweise, die das Backend versteht - etwa
    // 0x2001ff80 oder, wenn es Symbole gibt, &buffer.
    [ObservableProperty] private string _address = string.Empty;

    // Anzahl der zu lesenden Bytes. Vier ist ein Maschinenwort auf dem SVNR und damit die
    // Groesse, die man am haeufigsten sehen will.
    [ObservableProperty] private int _length = 4;

    // Die gelesenen Bytes, oder ein Hinweis, warum nichts gelesen werden konnte.
    [ObservableProperty] private string _value = string.Empty;
}
