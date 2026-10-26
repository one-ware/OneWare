using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Debugger.ViewModels;

namespace OneWare.Debugger.Models;

public partial class ValueEdit(InspectorEditSession session) : ObservableObject
{
    [ObservableProperty] private bool _isEditing;

    [ObservableProperty] private string _text = string.Empty;

    [ObservableProperty] private string? _error;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Hint))]
    private string? _lockReason;

    public InspectorEditSession Session { get; } = session;

    public int Bits { get; set; }

    public string Hint => LockReason ?? "Edit value";

    partial void OnLockReasonChanged(string? value)
    {
        Session.BeginEditCommand.NotifyCanExecuteChanged();
    }
}
