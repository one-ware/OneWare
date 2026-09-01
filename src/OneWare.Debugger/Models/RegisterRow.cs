using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

//    Eine Zeile im Registers-Reiter: Name des Registers und sein zuletzt gelesener Wert.
public partial class RegisterRow : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _value = string.Empty;

    // Der Wert, wie GDB ihn geliefert hat (Hex). Siehe MemoryRow.Raw.
    public string Raw { get; set; } = string.Empty;
}
