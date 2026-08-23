using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels.Inspector;

// Reiter "Variables" im Debugger-Panel: Variablen des aktuellen Stack-Frames,
// benannt nach der gleichnamigen Eclipse-Ansicht.
// Zieht sich nichts selbst, sondern zeigt an, was die Session beim letzten Halt mitgeliefert
// hat. Damit koennen Variablen und Register nie aus verschiedenen Halts stammen.
public partial class VariablesViewModel : ObservableObject
{
    private readonly IDebuggerService _debuggerService;

    public VariablesViewModel(IDebuggerService debuggerService)
    {
        _debuggerService = debuggerService;
        _debuggerService.StateChanged += (_, _) => Apply(_debuggerService.State);
    }

    public ObservableCollection<VariableRow> Variables { get; } = [];

    // Uebernimmt den zuletzt gemeldeten Zustand erneut. Waehrend das Ziel laeuft, gibt es
    // keinen Frame, aus dem sich etwas lesen liesse - deshalb ist der Befehl dann deaktiviert.
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private void Refresh()
    {
        Apply(_debuggerService.State);
    }

    private bool CanRefresh()
    {
        return _debuggerService.IsActive && !_debuggerService.State.IsRunning;
    }

    // Aktualisiert die Zeilen an Ort und Stelle, solange Name und Reihenfolge gleich bleiben.
    // Die Sammlung bei jedem Einzelschritt neu aufzubauen wuerde Auswahl und Bildlaufposition
    // verlieren.
    private void Apply(DebugSessionState state)
    {
        RefreshCommand.NotifyCanExecuteChanged();

        var locals = state.Locals;

        for (var i = 0; i < locals.Count; i++)
        {
            if (i < Variables.Count && Variables[i].Name == locals[i].Name)
            {
                Variables[i].Value = locals[i].Value;
                Variables[i].Type = locals[i].TypeName ?? string.Empty;
                continue;
            }

            var row = new VariableRow
            {
                Name = locals[i].Name,
                Value = locals[i].Value,
                Type = locals[i].TypeName ?? string.Empty
            };

            if (i < Variables.Count) Variables[i] = row;
            else Variables.Add(row);
        }

        while (Variables.Count > locals.Count) Variables.RemoveAt(Variables.Count - 1);
    }
}
