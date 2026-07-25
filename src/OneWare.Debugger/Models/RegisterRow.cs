using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

/// <summary>
///     Eine Zeile im Registers-Reiter: Name des Registers und sein zuletzt gelesener Wert.
/// </summary>
public partial class RegisterRow : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _value = string.Empty;
}
