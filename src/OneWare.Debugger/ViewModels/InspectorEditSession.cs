using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Debugger.ViewModels.Main;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels;

public partial class InspectorEditSession : ObservableObject
{
    private readonly IDebuggerService _debuggerService;
    private readonly ValueFormatViewModel _valueFormat;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEditing))]
    private IEditableValueRow? _current;

    public InspectorEditSession(IDebuggerService debuggerService, ValueFormatViewModel valueFormat)
    {
        _debuggerService = debuggerService;
        _valueFormat = valueFormat;

        _debuggerService.StateChanged += (_, _) => OnDebuggerStateChanged();
    }

    public bool IsEditing => Current != null;

    [RelayCommand(CanExecute = nameof(CanBeginEdit))]
    private void BeginEdit(IEditableValueRow? row)
    {
        if (row == null || !CanBeginEdit(row)) return;

        row.Edit.Text = row.Value;
        row.Edit.Error = null;
        row.Edit.IsEditing = true;
        Current = row;
    }

    private bool CanBeginEdit(IEditableValueRow? row)
    {
        return row is { Edit.LockReason: null } &&
               Current == null &&
               _debuggerService.IsActive &&
               !_debuggerService.State.IsRunning;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Current is not { } row) return;

        if (!ValueInputParser.TryParse(row.Edit.Text, _valueFormat.SelectedBase, row.Edit.Bits, out var value,
                out var error))
        {
            row.Edit.Error = error;
            return;
        }

        var result = await WriteValueAsync(row, value);
        if (!ReferenceEquals(Current, row)) return;

        if (!result.Succeeded)
        {
            row.Edit.Error = result.Message ?? "The target did not take the value.";
            return;
        }

        End(row);
    }

    [RelayCommand]
    private void Cancel()
    {
        if (Current is { } row) End(row);
    }

    public Task<DebugWriteResult> WriteValueAsync(IEditableValueRow row, ulong value)
    {
        var profile = _debuggerService.TargetProfile;
        var unitBytes = Math.Max(1, profile.AddressableUnitBytes);

        return row switch
        {
            RegisterRow register => _debuggerService.WriteRegisterAsync(register.Name, value),

            MemoryRow memory => _debuggerService.WriteMemoryAsync(
                MemoryTabViewModel.ToBackendAddress(memory.Address, unitBytes),
                ToBytes(value, unitBytes, profile.IsLittleEndian)),

            VariableRow variable => _debuggerService.WriteVariableAsync(variable.Name,
                SignExtend(value, variable.Edit.Bits)),

            _ => Task.FromResult(DebugWriteResult.Failed("This value cannot be edited."))
        };
    }

    partial void OnCurrentChanged(IEditableValueRow? value)
    {
        _valueFormat.IsLocked = value != null;
        BeginEditCommand.NotifyCanExecuteChanged();
    }

    private void OnDebuggerStateChanged()
    {
        if (!_debuggerService.IsActive || _debuggerService.State.IsRunning) Cancel();

        BeginEditCommand.NotifyCanExecuteChanged();
    }

    private void End(IEditableValueRow row)
    {
        row.Edit.IsEditing = false;
        row.Edit.Error = null;
        row.Edit.Text = string.Empty;
        Current = null;
    }

    private static byte[] ToBytes(ulong value, int count, bool littleEndian)
    {
        var bytes = new byte[count];

        for (var i = 0; i < Math.Min(count, sizeof(ulong)); i++) bytes[i] = (byte)(value >> (8 * i));

        if (!littleEndian) Array.Reverse(bytes);

        return bytes;
    }

    private static long SignExtend(ulong value, int bits)
    {
        if (bits is < 1 or >= 64) return unchecked((long)value);

        var signBit = 1UL << (bits - 1);

        return (value & signBit) == 0 ? (long)value : unchecked((long)(value | ~((signBit << 1) - 1)));
    }
}
