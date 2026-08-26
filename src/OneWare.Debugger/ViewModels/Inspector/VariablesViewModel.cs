using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
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

    // Aktualisiert die Zeilen an Ort und Stelle, solange Name und Reihenfolge gleich bleiben.
    // Die Sammlung bei jedem Einzelschritt neu aufzubauen wuerde Auswahl und Bildlaufposition
    // verlieren.
    private void Apply(DebugSessionState state)
    {
        var locals = state.Locals;

        for (var i = 0; i < locals.Count; i++)
        {
            var displayName = FormatTypeName(locals[i].Name);

            // TODO: Datentyp aus der SVD-Extension beziehen, sobald die Hardware-Info dort verfuegbar ist.
            var displayType = ResolveDataType(locals[i]);

            if (i < Variables.Count && Variables[i].Name == displayName)
            {
                Variables[i].Value = locals[i].Value;
                Variables[i].Type = displayType;
                continue;
            }

            var row = new VariableRow
            {
                Name = displayName,
                Value = locals[i].Value,
                Type = displayType
            };

            if (i < Variables.Count) Variables[i] = row;
            else Variables.Add(row);
        }

        while (Variables.Count > locals.Count) Variables.RemoveAt(Variables.Count - 1);
    }
    
    // Stub, bis die SVD-Extension den echten Datentyp pro Symbol liefert. Bis dahin bekommen
    // alle Zeilen "integer", damit die Spalte nicht leer bleibt.
    private static string ResolveDataType(DebugVariable _) => "integer";

    private static readonly Regex AddressPattern =
        new(@"_at_?(?<address>0x[0-9a-fA-F]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string FormatTypeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var match = AddressPattern.Match(raw);
        if (!match.Success) return raw;

        var normalized = raw.ToLowerInvariant();
        var isConst = normalized.Contains("read_only");
        var isPointer = normalized.Contains("pointer");

        var adress = match.Groups["address"].Value;
        return (isConst, isPointer) switch
        {
            (true, true) =>   "Read Only Pointer @ " + adress,
            (true, false) =>  "Constant @ " + adress,
            (false, true) =>  "Pointer @ " + adress,
            (false, false) => "Variable @ " + adress
        };
    }
}
