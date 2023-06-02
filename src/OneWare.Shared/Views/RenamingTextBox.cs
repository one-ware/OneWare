using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;


namespace OneWare.Shared.Views;

public class RenamingTextBox : TextBox, IStyleable
{
    public static readonly StyledProperty<string> InitialTextProperty =
        AvaloniaProperty.Register<RenamingTextBox, string >(nameof(InitialText));
    
    public static readonly StyledProperty<RelayCommand<string>> RequestRenameProperty =
        AvaloniaProperty.Register<RenamingTextBox, RelayCommand<string>>(nameof(InitialText));

    Type IStyleable.StyleKey => typeof(TextBox);
    
    public string InitialText
    {
        get => GetValue(InitialTextProperty);
        set
        {
            SetValue(InitialTextProperty, value);
            SetValue(TextProperty, value);
        }
    }

    public RelayCommand<string> RequestRename
    {
        get => GetValue(RequestRenameProperty);
        set => SetValue(RequestRenameProperty, value);
    }

    public void StartRename()
    {
        if (Text == null) return;

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
            RequestRename?.Execute(Text);
            Reset();
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
        Text = InitialText;
    }
}