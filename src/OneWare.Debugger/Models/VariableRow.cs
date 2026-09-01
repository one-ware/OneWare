using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// Eine Zeile im Variables-Panel: Name, aktueller Wert und Typ einer Variablen
// im gerade ausgewaehlten Stack-Frame.
public partial class VariableRow : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _type = string.Empty;

    [ObservableProperty] private string _value = string.Empty;

    // Der Wert, wie GDB ihn aus dem DWARF-Typ gerendert hat. Siehe MemoryRow.Raw.
    public string Raw { get; set; } = string.Empty;
}
