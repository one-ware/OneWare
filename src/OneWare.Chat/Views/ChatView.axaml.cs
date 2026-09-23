using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ColorTextBlock.Avalonia;
using OneWare.Chat.ViewModels;
using OneWare.Essentials.Services;

namespace OneWare.Chat.Views;

public partial class ChatView : UserControl
{
    private CompositeDisposable _disposables = new();
    private string _contextMenuSelection = string.Empty;
    
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
        if (e.Key is not (Key.Enter or Key.Return or Key.V)) return;
        if (DataContext is not ChatViewModel vm) return;

        // --- Ctrl+V: try image paste first ---
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && vm.SelectedChatService is IChatService chatService)
        {
            // Mark as handled immediately (synchronously) to prevent the TextBox from
            // processing the keystroke before our async clipboard check completes.
            e.Handled = true;
            _ = HandleClipboardPasteAsync(chatService);
            return;
        }

        if (e.Key is not (Key.Enter or Key.Return)) return;

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

    private async Task HandleClipboardPasteAsync(IChatService chatService)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        try
        {
            var formats = await clipboard.GetFormatsAsync();

            // Check for image data first.
            var imageFormat = formats.FirstOrDefault(f =>
                string.Equals(f, "PNG", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f, "image/png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f, "image/jpeg", StringComparison.OrdinalIgnoreCase));

            if (imageFormat != null)
            {
                var raw = await clipboard.GetDataAsync(imageFormat);
                if (raw is byte[] bytes && bytes.Length > 0)
                {
                    var mimeType = imageFormat.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                        ? "image/jpeg"
                        : "image/png";
                    var ext = mimeType == "image/jpeg" ? ".jpg" : ".png";
                    var name = $"image{ext}";

                    if (chatService.TryAddImageAttachment(bytes, mimeType, name))
                        return;
                }
            }
        }
        catch
        {
            // Clipboard access failed — fall through to text paste.
        }

        // No image (or service doesn't support it): fall back to pasting text.
        await FallbackTextPasteAsync();
    }

    private async Task FallbackTextPasteAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        try
        {
            var text = await clipboard.GetTextAsync();
            if (string.IsNullOrEmpty(text)) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var start = Math.Min(CommandBox.SelectionStart, CommandBox.SelectionEnd);
                var end = Math.Max(CommandBox.SelectionStart, CommandBox.SelectionEnd);
                var current = CommandBox.Text ?? string.Empty;
                CommandBox.Text = current.Remove(start, end - start).Insert(start, text);
                CommandBox.CaretIndex = start + text.Length;
                CommandBox.SelectionStart = CommandBox.SelectionEnd = CommandBox.CaretIndex;
            });
        }
        catch
        {
            // Best-effort; ignore clipboard errors.
        }
    }

    private static void Execute(System.Windows.Input.ICommand command, KeyEventArgs e)
    {
        e.Handled = true;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnMessagesContextFlyoutOpening(object? sender, EventArgs e)
    {
        // The selection is captured while the flyout opens: a right click outside an existing selection
        // collapses it (SelectableTextBlock does this after the context menu was requested), so reading
        // it again when the menu item is clicked can come up empty.
        _contextMenuSelection = GetSelectedMessageText();

        if (sender is MenuFlyout { Items.Count: > 0 } flyout && flyout.Items[0] is MenuItem copyItem)
            copyItem.IsEnabled = _contextMenuSelection.Length > 0;
    }

    private void OnCopySelectionClick(object? sender, RoutedEventArgs e)
    {
        if (_contextMenuSelection.Length == 0) return;

        TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(_contextMenuSelection);
    }

    /// <summary>
    /// Collects the selected text of the conversation. Messages are rendered by different controls
    /// (markdown, selectable text blocks and code editors), each keeping its own selection, so the
    /// selections of all realized message controls are concatenated in visual order.
    /// </summary>
    private string GetSelectedMessageText()
    {
        var builder = new StringBuilder();

        foreach (var visual in ItemsControl.GetVisualDescendants())
        {
            var selection = visual switch
            {
                SelectableTextBlock selectableTextBlock => selectableTextBlock.SelectedText,
                CTextBlock colorTextBlock => colorTextBlock.GetSelectedText(),
                TextEditor { SelectionLength: > 0 } textEditor => textEditor.SelectedText,
                _ => null
            };

            if (string.IsNullOrEmpty(selection)) continue;

            if (builder.Length > 0) builder.Append('\n');
            builder.Append(selection);
        }

        return builder.ToString();
    }
}