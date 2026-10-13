using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OneWare.Debugger.ViewModels.Main;

namespace OneWare.Debugger.Views.Main;

public partial class ConsoleTabView : UserControl
{
    private INotifyCollectionChanged? _linesSource;

    public ConsoleTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    // Keeps the console at the bottom edge, so that new output stays visible without the
    // user having to scroll.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();

        if (DataContext is not ConsoleTabViewModel viewModel) return;

        _linesSource = viewModel.Lines;
        _linesSource.CollectionChanged += OnLinesChanged;
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // After adding, a new measure pass is needed first, otherwise the viewer jumps to
        // a position that does not exist yet.
        Dispatcher.UIThread.Post(DebuggerConsoleScroll.ScrollToEnd, DispatcherPriority.Background);
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

            var text = string.Join(Environment.NewLine, viewModel.Lines.Select(x => x.Text));

            await clipboard.SetTextAsync(text);
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
