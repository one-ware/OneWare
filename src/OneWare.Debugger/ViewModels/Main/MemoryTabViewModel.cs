using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels.Main;

// "Memory" tab: the memory addresses watched by the user.
// This tab needs the service not for events but only for reading - when to
// read is decided by DebuggerViewModel.
public partial class MemoryTabViewModel : ObservableObject
{
    private readonly IDebuggerService _debuggerService;
    private readonly InspectorEditSession _editSession;

    // Address taken from the input line and turned into a watch list entry
    // on adding.
    [ObservableProperty] private string _addressText = string.Empty;

    public MemoryTabViewModel(IDebuggerService debuggerService, ValueFormatViewModel valueFormat,
        InspectorEditSession editSession)
    {
        _debuggerService = debuggerService;
        _editSession = editSession;
        ValueFormat = valueFormat;

        SelectedRows.CollectionChanged += (_, _) => RemoveWatchCommand.NotifyCanExecuteChanged();

        _editSession.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InspectorEditSession.IsEditing)) RemoveWatchCommand.NotifyCanExecuteChanged();
        };

        // The only subscription of this tab, and it is not about reading: the watermark belongs
        // to the target and is only known with the session. When to read is still decided by
        // DebuggerViewModel.
        _debuggerService.StateChanged += (_, _) => OnPropertyChanged(nameof(AddressWatermark));

        // A different number base only relabels what has already been read.
        valueFormat.Changed += (_, _) => RenderAll();
    }

    // Number base and sign of the display. The same object also serves Registers and
    // Variables -> the Base selector in the debugger toolbar switches all three at once.
    public ValueFormatViewModel ValueFormat { get; }

    // Example address in the empty input field. Comes from the target -> the core no longer holds
    // an address of a particular machine.
    public string AddressWatermark => _debuggerService.TargetProfile.AddressWatermark;

    // Kept across sessions, so that the same addresses need not be typed in again
    // after a restart.
    public ObservableCollection<MemoryRow> Watches { get; } = [];

    public ObservableCollection<MemoryRow> SelectedRows { get; } = [];

    // Rereads the whole watch list. One after the other: the backend answers only
    // one command at a time anyway, and reading in list order keeps the display
    // comprehensible.
    public void Refresh()
    {
        _ = RefreshAllAsync();
    }

    // After a session ends the addresses remain, but their values are no longer worth
    // anything.
    public void ClearValues()
    {
        foreach (var row in Watches)
        {
            row.Raw = string.Empty;
            row.Value = string.Empty;
        }
    }

    // Adds the typed address to the watch list and reads it immediately, provided
    // the target is halted. Without the immediate read the new row would stay empty until the
    // next halt, and one would not know whether the address is readable at all.
    [RelayCommand]
    private async Task AddWatchAsync()
    {
        if (string.IsNullOrWhiteSpace(AddressText)) return;

        var row = new MemoryRow { Address = AddressText.Trim(), Edit = new ValueEdit(_editSession) };
        Watches.Add(row);
        AddressText = string.Empty;


        await RefreshRowAsync(row);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveWatch))]
    private void RemoveWatch()
    {
        foreach (var row in SelectedRows.ToArray()) Watches.Remove(row);

        SelectedRows.Clear();
    }

    private bool CanRemoveWatch()
    {
        return SelectedRows.Count > 0 && !_editSession.IsEditing;
    }

    // Empties the whole watch list instead of only the selection. A no-op on an empty
    // list, hence without its own CanExecute.
    [RelayCommand]
    private void ClearWatches()
    {
        _editSession.CancelCommand.Execute(null);

        Watches.Clear();
        SelectedRows.Clear();
    }

    private async Task RefreshRowAsync(MemoryRow row)
    {
        if (!_debuggerService.IsActive)
        {
            Show(row, string.Empty);
            return;
        }

        if (_debuggerService.State.IsRunning)
        {
            Show(row, "target running");
            return;
        }

        var profile = _debuggerService.TargetProfile;
        var unitBytes = profile.AddressableUnitBytes;

        row.Bytes = row.Length * unitBytes;

        var value = await _debuggerService.ReadMemoryAsync(
            ToBackendAddress(row.Address, unitBytes),
            row.Length * unitBytes);

        Show(row, value == null ? "unreadable" : GroupIntoUnits(value, unitBytes, profile.IsLittleEndian));
    }

    // Keeps raw value and display together. A note such as "unreadable" passes through the
    // formatter unchanged and afterwards reads exactly as before.
    private void Show(MemoryRow row, string raw)
    {
        row.Raw = raw;
        row.Value = ValueFormatter.FormatHexUnits(raw, ValueFormat.SelectedBase);
        row.Edit.Bits = ValueFormatter.HexBits(raw);
        row.Edit.LockReason = raw.Trim().Contains(' ') ? "Only a single unit can be edited"
            : row.Edit.Bits == 0 ? "There is no value to edit"
            : null;
    }

    private void RenderAll()
    {
        foreach (var row in Watches)
            row.Value = ValueFormatter.FormatHexUnits(row.Raw, ValueFormat.SelectedBase);
    }

    // Converts the typed
    // address from target units to bytes by letting the backend do the
    // arithmetic -> the core holds no address parser, and a symbol or an & expression stays valid.
    // The cast is needed because a pointer cannot be multiplied: GDB rejects "(x)*2" with
    // "Argument to arithmetic operation not a number", but not "((long)(x))*2".
    internal static string ToBackendAddress(string address, int unitBytes)
    {
        return unitBytes <= 1 ? address : $"((long)({address}))*{unitBytes}";
    }

    // Combines the individual bytes into units of the target. With little endian the
    // low-order byte comes first -> reverse for display, so that the number reads the way it is
    // written. Which order applies is stated by the target through the profile; this used to be
    // fixed to little endian, and a big-endian target would have shown every word byte-swapped.
    private static string GroupIntoUnits(string spacedBytes, int unitBytes, bool littleEndian)
    {
        if (unitBytes <= 1) return spacedBytes;

        var bytes = spacedBytes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var units = new List<string>();

        for (var i = 0; i < bytes.Length; i += unitBytes)
        {
            var unit = bytes.Skip(i).Take(Math.Min(unitBytes, bytes.Length - i));

            units.Add(string.Concat(littleEndian ? unit.Reverse() : unit));
        }

        return string.Join(' ', units);
    }

    private async Task RefreshAllAsync()
    {
        foreach (var row in Watches.ToArray()) await RefreshRowAsync(row);
    }
}
