using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// One row in the Variables panel: name, current value and type of a variable
// in the currently selected stack frame.
public partial class VariableRow : ObservableObject, IEditableValueRow
{
    public required ValueEdit Edit { get; init; }

    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _type = string.Empty;

    [ObservableProperty] private string _value = string.Empty;

    // The value as GDB rendered it from the DWARF type. See MemoryRow.Raw.
    public string Raw { get; set; } = string.Empty;
}
