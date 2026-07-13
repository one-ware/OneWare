using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OneWare.Chat.ViewModels;

namespace OneWare.Chat.Views;

public partial class ChatView : UserControl
{
    private CompositeDisposable _disposables = new();
    
    public ChatView()
    {
        InitializeComponent();

        // Handle Enter shortcuts on the tunnel so they win over the TextBox's own newline handling.
        CommandBox.AddHandler(KeyDownEvent, OnCommandBoxKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Scroll to the latest message when the view first becomes visible.
        ScrollToEndDeferred();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ChatViewModel chatViewModel)
        {
            _disposables.Dispose();
            _disposables = new CompositeDisposable();

            ScrollToEndDeferred();

            Observable.FromEventPattern(chatViewModel, nameof(chatViewModel.ContentAdded)).Subscribe(x =>
            {
                ScrollToEndDeferred();
            })
            .DisposeWith(_disposables);
        }
    }

    private void ScrollToEndDeferred()
    {
        // Defer so the scroll happens after the new content has been measured/arranged.
        Dispatcher.UIThread.Post(() => ScrollViewer.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void OnCommandBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return)) return;
        if (DataContext is not ChatViewModel vm) return;

        var modifiers = e.KeyModifiers;

        // Shift+Enter inserts a newline — let the TextBox handle it.
        if (modifiers.HasFlag(KeyModifiers.Shift)) return;

        // Ctrl+Enter steers, Alt+Enter queues (both only while the agent is busy).
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            Execute(vm.SteerCommand, e);
            return;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            Execute(vm.QueueCommand, e);
            return;
        }

        // Plain Enter: steer while busy, otherwise start a new turn.
        Execute(vm.IsBusy ? vm.SteerCommand : vm.SendCommand, e);
    }

    private static void Execute(System.Windows.Input.ICommand command, KeyEventArgs e)
    {
        e.Handled = true;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}