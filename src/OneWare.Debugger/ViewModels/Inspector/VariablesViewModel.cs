using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels.Inspector;

// "Variables" tab in the debugger panel: variables of the current stack frame,
// named after the Eclipse view of the same name.
// Fetches nothing itself, but shows what the session supplied with the last
// halt. Variables and registers can therefore never come from different halts.
public class VariablesViewModel : ObservableObject
{
    private readonly IDebuggerService _debuggerService;
    private readonly InspectorEditSession _editSession;

    public VariablesViewModel(IDebuggerService debuggerService, ValueFormatViewModel valueFormat,
        InspectorEditSession editSession)
    {
        _debuggerService = debuggerService;
        _editSession = editSession;
        ValueFormat = valueFormat;

        _debuggerService.StateChanged += (_, _) => Apply(_debuggerService.State);

        // A different number base only relabels what has already been read.
        valueFormat.Changed += (_, _) => RenderAll();
    }

    // Number base and sign of the display. The same object also serves Memory and
    // Registers -> the Base selector in the debugger toolbar switches all three at once.
    public ValueFormatViewModel ValueFormat { get; }

    public ObservableCollection<VariableRow> Variables { get; } = [];

    // Updates the rows in place as long as name and order stay the same.
    // Rebuilding the collection on every single step would lose selection and scroll
    // position.
    private void Apply(DebugSessionState sessionState)
    {
        // While the target runs, the contract reports no locals ("Empty while the target
        // runs"). Adopting that would mean emptying the table on every Continue and refilling it
        // on the next halt -> instead the last halt stays until a
        // new one delivers something else.
        // Deliberately tied to IsRunning and not to the count: a halt without locals - frame without
        // variables, program without symbols - is a real statement and must clear the table.
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
                Type = displayType,
                Edit = new ValueEdit(_editSession)
            };
            Show(row, variables[i].Value);

            if (i < Variables.Count) Variables[i] = row;
            else Variables.Add(row);
        }

        while (Variables.Count > variables.Count) Variables.RemoveAt(Variables.Count - 1);
    }

    // Keeps raw value and display together. Unlike Memory and Registers, the value does not
    // arrive raw here: GDB has already rendered it by the DWARF type, as a rule as a
    // signed decimal number. The bit pattern can be calculated back from it without
    // loss -> no additional command to the backend needed. Whatever is not a number,
    // such as a structure or a pointer with a symbol name, stays as it is.
    private void Show(VariableRow row, string raw)
    {
        row.Raw = raw;
        row.Value = ValueFormatter.FormatDecimalValue(raw, BitsFor(raw), ValueFormat.SelectedBase);
        row.Edit.Bits = BitsFor(raw);
        row.Edit.LockReason = long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? null
            : "Only whole numbers can be edited";
    }

    private void RenderAll()
    {
        foreach (var row in Variables)
            row.Value = ValueFormatter.FormatDecimalValue(row.Raw, BitsFor(row.Raw), ValueFormat.SelectedBase);
    }

    // Word width for the conversion. It follows the target's address unit from the profile -> the core
    // holds no width of a particular machine. If the value does not fit, it is doubled:
    // a 32-bit value on a byte-addressed machine would otherwise be cut off.
    private int BitsFor(string raw)
    {
        var bits = Math.Max(8, _debuggerService.TargetProfile.AddressableUnitBytes * 8);

        if (!long.TryParse(raw.Trim(), out var value)) return bits;

        while (bits < 64 && !FitsIn(value, bits)) bits *= 2;

        return bits;
    }

    // Generous towards both readings: -1 and 65535 should both count as a 16-bit
    // word, because GDB delivers one or the other depending on the DWARF type.
    private static bool FitsIn(long value, int bits)
    {
        return value >= -(1L << (bits - 1)) && value <= (1L << bits) - 1;
    }


    // The type the backend reported. This used to be fixed to "int16" - the type of exactly
    // one target, imposed on every variable of every target. If the backend reports none,
    // the column stays empty: an invented type is worse than a visibly unknown one.
    private static string ResolveDataType(DebugVariable variable) => variable.TypeName ?? string.Empty;
    
    
}
