using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace OneWare.Essentials.Controls;

public class RenamingTextBox : TextBox
{
    public static readonly StyledProperty<Action<Action<string>>?> RenameActionProperty =
        AvaloniaProperty.Register<RenamingTextBox, Action<Action<string>>?>(nameof(RenameAction), null, false, BindingMode.OneWayToSource);
    protected override Type StyleKeyOverride => typeof(TextBox);

    private Action<string>? _callback;
    private string? _backupText;

    public Action<Action<string>>? RenameAction
    {
        get => GetValue(RenameActionProperty);
        private set => SetValue(RenameActionProperty, value);
    }

    public RenamingTextBox()
    {
        RenameAction = StartRename;
    }

    public void StartRename(Action<string> callback)
    {
        _callback = callback;
        if (Text == null) return;

        _backupText = Text;

        IsEnabled = true;

        var length = Text.LastIndexOf(".", StringComparison.Ordinal);
        SelectionStart = 0;
        if (length < 1) SelectionEnd = Text.Length;
        else SelectionEnd = length;

        Dispatcher.UIThread.Post(() => Focus());
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Reset();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter && Text != null)
        {
            e.Handled = true;
            var newText = Text;
            Reset();
            _callback?.Invoke(newText);
        }

        if (e.Key is Key.Escape)
        {
            Reset();
        }
        base.OnKeyDown(e);
    }

    private void Reset()
    {
        IsEnabled = false;
        ClearSelection();
        Text = _backupText;
    }
}