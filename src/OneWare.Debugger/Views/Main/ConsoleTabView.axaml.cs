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

    // Haelt die Konsole am unteren Rand, damit neue Ausgaben sichtbar bleiben, ohne dass der
    // Benutzer scrollen muss.
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

        // Nach dem Hinzufuegen muss erst neu ausgemessen werden, sonst springt der Viewer an
        // eine Position, die es noch nicht gibt.
        Dispatcher.UIThread.Post(DebuggerConsoleScroll.ScrollToEnd, DispatcherPriority.Background);
    }

    // Legt den gesamten Verlauf als Text in die Zwischenablage. Zeilenweises Markieren geht
    // in der Ansicht nicht ueber Zeilengrenzen hinweg, und genau der ganze Verlauf ist das,
    // was man beim Melden eines Fehlers braucht.
    //
    // Im Code-Behind und nicht im ViewModel, weil die Zwischenablage am TopLevel haengt und
    // das ViewModel dafuer ein Control kennen muesste. Dieselbe Aufteilung wie in ChatView.
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
            // Eine fehlgeschlagene Zwischenablage ist kein Grund, die Anwendung zu beenden -
            // und weil async void hier nichts hat, wohin es die Ausnahme reichen koennte,
            // endet sie sonst im Prozessabbruch.
        }
    }

    private void Unsubscribe()
    {
        if (_linesSource != null) _linesSource.CollectionChanged -= OnLinesChanged;

        _linesSource = null;
    }
}
