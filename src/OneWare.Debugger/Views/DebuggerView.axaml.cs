using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using OneWare.Debugger.ViewModels;

namespace OneWare.Debugger.Views;

public partial class DebuggerView : UserControl
{
    private INotifyCollectionChanged? _consoleSource;
    private INotifyCollectionChanged? _debuggerConsoleSource;

    public DebuggerView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    ///     Haelt beide Konsolen am unteren Rand, damit neue Ausgaben sichtbar bleiben, ohne dass
    ///     der Benutzer scrollen muss.
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();

        if (DataContext is not DebuggerViewModel viewModel) return;

        _consoleSource = viewModel.Console;
        _debuggerConsoleSource = viewModel.DebuggerConsole;

        _consoleSource.CollectionChanged += OnConsoleChanged;
        _debuggerConsoleSource.CollectionChanged += OnDebuggerConsoleChanged;
    }

    private void OnConsoleChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToEnd(ConsoleScroll, e);
    }

    private void OnDebuggerConsoleChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToEnd(DebuggerConsoleScroll, e);
    }

    private static void ScrollToEnd(ScrollViewer? scrollViewer, NotifyCollectionChangedEventArgs e)
    {
        if (scrollViewer == null || e.Action != NotifyCollectionChangedAction.Add) return;

        // Nach dem Hinzufuegen muss erst neu ausgemessen werden, sonst springt der Viewer an
        // eine Position, die es noch nicht gibt.
        Dispatcher.UIThread.Post(scrollViewer.ScrollToEnd, DispatcherPriority.Background);
    }

    private void Unsubscribe()
    {
        if (_consoleSource != null) _consoleSource.CollectionChanged -= OnConsoleChanged;
        if (_debuggerConsoleSource != null) _debuggerConsoleSource.CollectionChanged -= OnDebuggerConsoleChanged;

        _consoleSource = null;
        _debuggerConsoleSource = null;
    }
}
