namespace OneWare.Debugger.Models;

// Zahlensystem, in dem Memory, Registers und Variables ihre Werte anzeigen.
// Nur Dec kennt ueberhaupt ein Vorzeichen -> siehe ValueFormatViewModel.IsSignedEnabled.
public enum NumberBase
{
    Hex,
    Dec,
    Oct,
    Bin
}
