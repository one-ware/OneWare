using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OneWare.Debugger.ViewModels.Main;

namespace OneWare.Debugger.Views.Main;

public partial class ConsoleTabView : UserControl
{
    private INotifyCollectionChanged? _linesSource;

    public ConsoleTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        // Tunnel: a single-line TextBox would otherwise use Up/Down to move the caret.
        CommandBox.AddHandler(KeyDownEvent, OnCommandBoxKeyDown, RoutingStrategies.Tunnel);
    }

    private ScrollViewer? Scroll => ConsoleItems.FindDescendantOfType<ScrollViewer>();

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();

        if (DataContext is not ConsoleTabViewModel viewModel) return;

        _linesSource = viewModel.Lines;
        _linesSource.CollectionChanged += OnLinesChanged;
    }

    // Follows new output while the view is at the bottom; leaves it alone while the user reads further up.
    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Dispatcher.UIThread.Post(() => Scroll?.ScrollToEnd(), DispatcherPriority.Background);
            return;
        }

        if (e.Action != NotifyCollectionChangedAction.Add || Scroll is not { } scroll) return;

        // Measured before the new line is laid out -> still describes the old end.
        var atBottom = scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - 24;
        if (atBottom) Dispatcher.UIThread.Post(scroll.ScrollToEnd, DispatcherPriority.Background);
    }

    private void OnCommandBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ConsoleTabViewModel viewModel) return;

        var direction = e.Key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            _ => 0
        };
        if (direction == 0) return;

        viewModel.BrowseHistory(direction);
        CommandBox.CaretIndex = CommandBox.Text?.Length ?? 0;
        e.Handled = true;
    }

    // Puts the whole history on the clipboard as text. Selecting line by line does not work
    // across line boundaries in the view, and the whole history is exactly what
    // is needed when reporting an error.
    //
    // In the code-behind and not in the view model, because the clipboard belongs to the TopLevel and
    // the view model would have to know a control for it. The same split as in ChatView.
    private async void OnCopyConsoleClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not ConsoleTabViewModel viewModel) return;

            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;

            await clipboard.SetTextAsync(viewModel.GetText());
        }
        catch (Exception)
        {
            // A failed clipboard is no reason to terminate the application -
            // and because async void has nowhere here to pass the exception to,
            // it otherwise ends in a process abort.
        }
    }

    private void Unsubscribe()
    {
        if (_linesSource != null) _linesSource.CollectionChanged -= OnLinesChanged;

        _linesSource = null;
    }
}
