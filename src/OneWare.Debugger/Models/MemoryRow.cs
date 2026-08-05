using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

/// <summary>
///     Eine Zeile im Memory-Reiter: eine vom Benutzer eingetragene Adresse und die zuletzt
///     dort gelesenen Bytes.
/// </summary>
/// <remarks>
///     <see cref="Address" /> und <see cref="Length" /> sind bearbeitbar, weil man beim Suchen
///     eines Speicherbereichs die Adresse laufend anpasst. Beide loesen ein erneutes Lesen aus,
///     sobald sie sich aendern.
/// </remarks>
public partial class MemoryRow : ObservableObject
{
    /// <summary>
    ///     Adresse oder Ausdruck, in der Schreibweise, die das Backend versteht - etwa
    ///     <c>0x2001ff80</c> oder, wenn es Symbole gibt, <c>&amp;buffer</c>.
    /// </summary>
    [ObservableProperty] private string _address = string.Empty;

    /// <summary>
    ///     Anzahl der zu lesenden Bytes. Vier ist ein Maschinenwort auf dem SVNR und damit die
    ///     Groesse, die man am haeufigsten sehen will.
    /// </summary>
    [ObservableProperty] private int _length = 4;

    /// <summary>
    ///     Die gelesenen Bytes, oder ein Hinweis, warum nichts gelesen werden konnte.
    /// </summary>
    [ObservableProperty] private string _value = string.Empty;
}
