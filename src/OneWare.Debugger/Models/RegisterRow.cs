using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// One row in the Registers tab: the register's name and its value as last read.
public partial class RegisterRow : ObservableObject, IEditableValueRow
{
    public required ValueEdit Edit { get; init; }

    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _value = string.Empty;

    // The value as GDB delivered it (hex). See MemoryRow.Raw.
    public string Raw { get; set; } = string.Empty;
}
