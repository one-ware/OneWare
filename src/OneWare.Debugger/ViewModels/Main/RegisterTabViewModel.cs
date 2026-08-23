using System.Collections.ObjectModel;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Debugger.ViewModels.Main;

// Reiter "Registers": die Registerinhalte des letzten Halts.
// Bekommt seinen Zustand von DebuggerViewModel gereicht und kennt weder Sitzung noch Dienst
// -> zum Pruefen genuegt eine Liste von RegisterValue, kein laufender Debugger.
public class RegisterTabViewModel
{
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
                Rows[i].Value = registers[i].Value;
                continue;
            }

            var row = new RegisterRow { Name = registers[i].Name, Value = registers[i].Value };

            if (i < Rows.Count) Rows[i] = row;
            else Rows.Add(row);
        }

        while (Rows.Count > registers.Count) Rows.RemoveAt(Rows.Count - 1);
    }

    public void Clear()
    {
        Rows.Clear();
    }
}
