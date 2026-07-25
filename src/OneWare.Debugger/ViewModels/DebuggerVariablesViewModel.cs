using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Rechtes Panel: Variablen des aktuellen Stack-Frames, benannt nach der gleichnamigen
///     Eclipse-Ansicht.
/// </summary>
public partial class DebuggerVariablesViewModel : ExtendedTool
{
    public const string IconKey = "Variable";

    public DebuggerVariablesViewModel() : base(IconKey)
    {
        Id = "DebuggerVariables";
        Title = "Variables";
    }

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
