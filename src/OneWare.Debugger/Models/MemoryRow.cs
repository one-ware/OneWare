using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

// One row in the Memory tab: an address entered by the user and the bytes last
// read there.
// The table only displays; rows appear and disappear through the buttons of the bar.
public partial class MemoryRow : ObservableObject, IEditableValueRow
{
    public required ValueEdit Edit { get; init; }

    // Address or expression in the notation the backend understands - a number, or a
    // symbol such as &buffer. Counted in addressable units of the target, not in bytes;
    // the tab does the conversion based on the profile.
    [ObservableProperty] private string _address = string.Empty;

    // Number of addressable units to read - bytes on a byte-addressed
    // machine, otherwise its word width. A new row initially reads one unit.
    [ObservableProperty] private int _length = 1;

    // How many bytes the row covers - Length times the width of one unit. For display only;
    // reading still goes through Length.
    [ObservableProperty] private int _bytes = 1;

    // The bytes read, or a note on why nothing could be read.
    [ObservableProperty] private string _value = string.Empty;

    // The same as Value, but in the form in which it came from the backend: units in hex.
    // Kept so that a change of number base only relabels the row, instead of
    // having to read the target again for it -> which would not work at all during a run.
    public string Raw { get; set; } = string.Empty;
}
