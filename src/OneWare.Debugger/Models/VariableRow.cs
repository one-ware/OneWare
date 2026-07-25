using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

/// <summary>
///     Eine Zeile im Variables-Panel: Name, aktueller Wert und Typ einer Variablen
///     im gerade ausgewaehlten Stack-Frame.
/// </summary>
public partial class VariableRow : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _type = string.Empty;

    [ObservableProperty] private string _value = string.Empty;
}
