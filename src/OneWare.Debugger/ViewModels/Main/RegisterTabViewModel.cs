using System.Collections.ObjectModel;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Entities;

namespace OneWare.Debugger.ViewModels.Main;

// "Registers" tab: the register contents of the last halt.
// Is handed its state by DebuggerViewModel and knows neither session nor service
// -> a list of DebugRegisterValue and a switch suffice for testing, no running debugger.
public class RegisterTabViewModel
{
    private readonly InspectorEditSession _editSession;

    public RegisterTabViewModel(ValueFormatViewModel valueFormat, InspectorEditSession editSession)
    {
        ValueFormat = valueFormat;
        _editSession = editSession;

        // A different number base only relabels what has already been read.
        valueFormat.Changed += (_, _) => RenderAll();
    }

    // Number base and sign of the display. The same object also serves Memory and
    // Variables -> the Base selector in the debugger toolbar switches all three at once.
    public ValueFormatViewModel ValueFormat { get; }

    // Register contents as read at the last halt.
    public ObservableCollection<RegisterRow> Rows { get; } = [];

    // Updates the register display in place, so that the scrollbar does not jump to the
    // top on every F10 or Continue.
    public void Apply(IReadOnlyList<DebugRegisterValue> registers, bool isRunning,
        IReadOnlyList<string>? readOnlyRegisters)
    {
        // While the target runs, the list is empty (DebugSessionState.Registers) -> the
        // rows of the last halt stay, instead of disappearing on every Continue.
        // This used to be a check on the count. It caught the same case, but also
        // a second one: a backend that cannot read registers at all likewise reports nothing
        // at a real halt - and then the rows of the previous halt would remain as a
        // claim. IsRunning keeps the two cases apart.
        if (isRunning) return;

        for (var i = 0; i < registers.Count; i++)
        {
            var isReadOnly = readOnlyRegisters?.Contains(registers[i].Name) == true;

            if (i < Rows.Count && Rows[i].Name == registers[i].Name)
            {
                Show(Rows[i], registers[i].Value, isReadOnly);
                continue;
            }

            var row = new RegisterRow { Name = registers[i].Name, Edit = new ValueEdit(_editSession) };
            Show(row, registers[i].Value, isReadOnly);

            if (i < Rows.Count) Rows[i] = row;
            else Rows.Add(row);
        }

        while (Rows.Count > registers.Count) Rows.RemoveAt(Rows.Count - 1);
    }

    public void Clear()
    {
        Rows.Clear();
    }

    // Keeps raw value and display together. The word width is in the hex value itself, which GDB
    // pads to the register width -> a 32-bit register is not read as signed like a
    // 16-bit word.
    private void Show(RegisterRow row, string raw, bool isReadOnly)
    {
        row.Raw = raw;
        row.Value = ValueFormatter.FormatHexValue(raw, ValueFormat.SelectedBase);
        row.Edit.Bits = ValueFormatter.HexBits(raw);
        row.Edit.LockReason = isReadOnly ? "Read-only register"
            : row.Edit.Bits == 0 ? "There is no value to edit"
            : null;
    }

    private void RenderAll()
    {
        foreach (var row in Rows)
            row.Value = ValueFormatter.FormatHexValue(row.Raw, ValueFormat.SelectedBase);
    }
}
