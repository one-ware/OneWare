using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

/// <summary>
///     Eine Zeile im Expressions-Panel: ein vom Benutzer eingegebener Ausdruck und der Wert,
///     zu dem die laufende Session ihn aufgeloest hat.
/// </summary>
public partial class ExpressionRow : ObservableObject
{
    [ObservableProperty] private string _expression = string.Empty;

    [ObservableProperty] private string _value = string.Empty;
}
