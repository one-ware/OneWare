using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Reiter "Variables" im Debugging-Panel: Variablen des aktuellen Stack-Frames,
///     benannt nach der gleichnamigen Eclipse-Ansicht.
/// </summary>
public partial class DebuggerVariablesViewModel : ObservableObject
{
    public ObservableCollection<VariableRow> Variables { get; } = [];

    /// <summary>
    ///     Liest die Variablen neu aus. Ohne laufende Session gibt es nichts zu lesen,
    ///     deshalb ist der Befehl bis zur Anbindung der Session deaktiviert.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private void Refresh()
    {
    }

    private bool CanRefresh()
    {
        return false;
    }
}
