using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Debugger.Helpers;
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

    public VariablesViewModel(IDebuggerService debuggerService, ValueFormatViewModel valueFormat)
    {
        _debuggerService = debuggerService;
        ValueFormat = valueFormat;

        _debuggerService.StateChanged += (_, _) => Apply(_debuggerService.State);

        // Ein anderes Zahlensystem beschriftet nur neu, was schon gelesen ist.
        valueFormat.Changed += (_, _) => RenderAll();
    }

    // Zahlensystem und Vorzeichen der Anzeige. Dasselbe Objekt bedienen auch Memory und
    // Registers -> die Leiste ueber der Tabelle schaltet alle drei zugleich um.
    public ValueFormatViewModel ValueFormat { get; }

    public ObservableCollection<VariableRow> Variables { get; } = [];

    // Aktualisiert die Zeilen an Ort und Stelle, solange Name und Reihenfolge gleich bleiben.
    // Die Sammlung bei jedem Einzelschritt neu aufzubauen wuerde Auswahl und Bildlaufposition
    // verlieren.
    private void Apply(DebugSessionState sessionState)
    {
        // Waehrend das Ziel laeuft, meldet der Vertrag keine Locals ("Empty while the target
        // runs"). Das zu uebernehmen hiesse, die Tabelle bei jedem Continue zu leeren und beim
        // naechsten Halt neu zu fuellen -> stattdessen bleibt der letzte Halt stehen, bis ein
        // neuer etwas anderes liefert.
        // Bewusst an IsRunning und nicht an der Anzahl: ein Halt ohne Locals - Frame ohne
        // Variablen, Programm ohne Symbole - ist eine echte Aussage und muss raeumen.
        if (sessionState.IsRunning) return;

        var variables = sessionState.Locals;

        for (var i = 0; i < variables.Count; i++)
        {
            var displayName = variables[i].Name;

            var displayType = ResolveDataType(variables[i]);

            if (i < Variables.Count && Variables[i].Name == displayName)
            {
                Show(Variables[i], variables[i].Value);
                Variables[i].Type = displayType;
                continue;
            }

            var row = new VariableRow
            {
                Name = displayName,
                Type = displayType
            };
            Show(row, variables[i].Value);

            if (i < Variables.Count) Variables[i] = row;
            else Variables.Add(row);
        }

        while (Variables.Count > variables.Count) Variables.RemoveAt(Variables.Count - 1);
    }

    // Haelt Rohwert und Anzeige beisammen. Anders als bei Memory und Registers kommt der Wert
    // hier nicht roh an: GDB hat ihn schon anhand des DWARF-Typs gerendert, in aller Regel als
    // vorzeichenbehaftete Dezimalzahl. Aus der laesst sich das Bitmuster verlustfrei
    // zurueckrechnen -> kein zusaetzliches Kommando ans Backend noetig. Was keine Zahl ist,
    // etwa eine Struktur oder ein Zeiger mit Symbolnamen, bleibt stehen.
    private void Show(VariableRow row, string raw)
    {
        row.Raw = raw;
        row.Value = ValueFormatter.FormatDecimalValue(raw, BitsFor(raw), ValueFormat.SelectedBase,
            ValueFormat.IsSigned);
    }

    private void RenderAll()
    {
        foreach (var row in Variables)
            row.Value = ValueFormatter.FormatDecimalValue(row.Raw, BitsFor(row.Raw), ValueFormat.SelectedBase,
                ValueFormat.IsSigned);
    }

    // Wortbreite fuer die Umrechnung. Sie kommt als Angabe des Ziels aus dem Profil -> im Kern
    // steht keine Breite einer bestimmten Maschine. Passt der Wert nicht hinein, wird verdoppelt:
    // ein 32-Bit-Wert auf einer byteadressierten Maschine wuerde sonst abgeschnitten.
    private int BitsFor(string raw)
    {
        var bits = Math.Max(8, _debuggerService.MemoryProfile.WordBits);

        if (!long.TryParse(raw.Trim(), out var value)) return bits;

        while (bits < 64 && !FitsIn(value, bits)) bits *= 2;

        return bits;
    }

    // Grosszuegig gegenueber beiden Deutungen: -1 und 65535 sollen beide als 16-Bit-Wort
    // gelten, weil GDB je nach DWARF-Typ das eine oder das andere liefert.
    private static bool FitsIn(long value, int bits)
    {
        return value >= -(1L << (bits - 1)) && value <= (1L << bits) - 1;
    }


    // Der Typ, den das Backend gemeldet hat. Frueher stand hier fest "int16" - der Typ genau
    // eines Ziels, jeder Variablen jedes Ziels untergeschoben. Meldet das Backend keinen, bleibt
    // die Spalte leer: ein erfundener Typ ist schlechter als ein sichtbar unbekannter.
    private static string ResolveDataType(DebugVariable variable) => variable.TypeName ?? string.Empty;
    
    
}
