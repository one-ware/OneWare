using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
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

    private async void OnCommandBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ChatViewModel vm) return;

        var modifiers = e.KeyModifiers;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.PlatformSettings?.HotkeyConfiguration.Paste.Any(gesture => gesture.Matches(e)) == true)
        {
            e.Handled = true;
            try
            {
                await PasteClipboardAsync(vm, topLevel);
            }
            catch (TimeoutException)
            {
                // Match Avalonia TextBox behavior when the platform clipboard is temporarily busy.
            }

            return;
        }

        if (e.Key is not (Key.Enter or Key.Return)) return;

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

    private async Task PasteClipboardAsync(ChatViewModel viewModel, TopLevel topLevel)
    {
        if (topLevel.Clipboard == null) return;

        if (viewModel.SelectedChatService != null &&
            await viewModel.SelectedChatService.TryAddClipboardAttachmentAsync(topLevel))
            return;

        var clipboardText = await topLevel.Clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(clipboardText)) return;

        var text = CommandBox.Text ?? string.Empty;
        var selectionStart = Math.Min(CommandBox.SelectionStart, CommandBox.SelectionEnd);
        var selectionEnd = Math.Max(CommandBox.SelectionStart, CommandBox.SelectionEnd);
        CommandBox.Text = text[..selectionStart] + clipboardText + text[selectionEnd..];
        CommandBox.CaretIndex = selectionStart + clipboardText.Length;
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