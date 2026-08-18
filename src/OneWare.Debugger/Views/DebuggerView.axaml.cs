using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using OneWare.Debugger.ViewModels;

namespace OneWare.Debugger.Views;

public partial class DebuggerView : UserControl
{
    private INotifyCollectionChanged? _debuggerConsoleSource;

    public DebuggerView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    // Haelt die Konsole am unteren Rand, damit neue Ausgaben sichtbar bleiben, ohne dass der
    // Benutzer scrollen muss.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();

        if (DataContext is not DebuggerViewModel viewModel) return;

        _debuggerConsoleSource = viewModel.DebuggerConsole;
        _debuggerConsoleSource.CollectionChanged += OnDebuggerConsoleChanged;
    }

    private void OnDebuggerConsoleChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // Nach dem Hinzufuegen muss erst neu ausgemessen werden, sonst springt der Viewer an
        // eine Position, die es noch nicht gibt.
        Dispatcher.UIThread.Post(DebuggerConsoleScroll.ScrollToEnd, DispatcherPriority.Background);
    }

    private void Unsubscribe()
    {
        if (_debuggerConsoleSource != null) _debuggerConsoleSource.CollectionChanged -= OnDebuggerConsoleChanged;

        _debuggerConsoleSource = null;
    }
}
