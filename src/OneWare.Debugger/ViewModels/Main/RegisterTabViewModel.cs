using System.Collections.ObjectModel;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Debugger.ViewModels.Main;

// Reiter "Registers": die Registerinhalte des letzten Halts.
// Bekommt seinen Zustand von DebuggerViewModel gereicht und kennt weder Sitzung noch Dienst
// -> zum Pruefen genuegt eine Liste von RegisterValue, kein laufender Debugger.
public class RegisterTabViewModel
{
    public RegisterTabViewModel(ValueFormatViewModel valueFormat)
    {
        ValueFormat = valueFormat;

        // Ein anderes Zahlensystem beschriftet nur neu, was schon gelesen ist.
        valueFormat.Changed += (_, _) => RenderAll();
    }

    // Zahlensystem und Vorzeichen der Anzeige. Dasselbe Objekt bedienen auch Memory und
    // Variables -> die Leiste ueber der Tabelle schaltet alle drei zugleich um.
    public ValueFormatViewModel ValueFormat { get; }

    // Registerinhalte, wie sie beim letzten Halt gelesen wurden.
    public ObservableCollection<RegisterRow> Rows { get; } = [];

    // Aktualisiert die Registeranzeige an Ort und Stelle. Damit die Scrollbar sich nicht bei
    // jedem F10 bzw. Continue hoch springt
    public void Apply(IReadOnlyList<RegisterValue> registers)
    {
        if (registers.Count == 0) return;

        for (var i = 0; i < registers.Count; i++)
        {
            if (i < Rows.Count && Rows[i].Name == registers[i].Name)
            {
                Show(Rows[i], registers[i].Value);
                continue;
            }

            var row = new RegisterRow { Name = registers[i].Name };
            Show(row, registers[i].Value);

            if (i < Rows.Count) Rows[i] = row;
            else Rows.Add(row);
        }

        while (Rows.Count > registers.Count) Rows.RemoveAt(Rows.Count - 1);
    }

    public void Clear()
    {
        Rows.Clear();
    }

    // Haelt Rohwert und Anzeige beisammen. Die Wortbreite steht im Hexwert selbst, den GDB auf
    // die Registerbreite auffuellt -> ein 32-Bit-Register wird nicht wie ein 16-Bit-Wort
    // vorzeichenbehaftet gelesen.
    private void Show(RegisterRow row, string raw)
    {
        row.Raw = raw;
        row.Value = ValueFormatter.FormatHexValue(raw, ValueFormat.SelectedBase, ValueFormat.IsSigned);
    }

    private void RenderAll()
    {
        foreach (var row in Rows)
            row.Value = ValueFormatter.FormatHexValue(row.Raw, ValueFormat.SelectedBase, ValueFormat.IsSigned);
    }
}
