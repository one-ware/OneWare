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

        ScrollViewer.ScrollChanged += OnMessagesScrollChanged;
    }

    // Whether the conversation follows its end. Content that grows after it was added (a tool block that
    // expands, streamed terminal output, items realized by virtualization) keeps the end in view, unless the
    // user scrolled away from it.
    private bool _followEnd = true;
    private const double FollowEndTolerance = 24;

    private void OnMessagesScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            if (_followEnd) ScrollViewer.ScrollToEnd();
            return;
        }

        if (e.OffsetDelta.Y != 0)
            _followEnd = ScrollViewer.Offset.Y >= ScrollViewer.Extent.Height - ScrollViewer.Viewport.Height - FollowEndTolerance;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Scroll to the latest message when the view first becomes visible.
        ScrollToEndDeferred();
    }

    private ChatViewModel? _chatViewModel;
    private ChatSessionViewModel? _observedTab;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_chatViewModel != null) _chatViewModel.PropertyChanged -= OnChatViewModelPropertyChanged;
        _chatViewModel = DataContext as ChatViewModel;
        if (_chatViewModel != null) _chatViewModel.PropertyChanged += OnChatViewModelPropertyChanged;

        ObserveTab(_chatViewModel?.CurrentSession);
    }

    private void OnChatViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.CurrentSession)) ObserveTab(_chatViewModel?.CurrentSession);
    }

    /// <summary>Follows the new content of the shown chat and scrolls to its end when switching chats.</summary>
    private void ObserveTab(ChatSessionViewModel? tab)
    {
        if (ReferenceEquals(tab, _observedTab)) return;

        _disposables.Dispose();
        _disposables = new CompositeDisposable();
        _observedTab = tab;

        ScrollToEndDeferred();

        if (tab == null) return;

        Observable.FromEventPattern(tab, nameof(tab.ContentAdded))
            .Subscribe(_ => ScrollToEndDeferred())
            .DisposeWith(_disposables);
    }

    private void ScrollToEndDeferred()
    {
        // Defer so the scroll happens after the new content has been measured/arranged.
        _followEnd = true;
        Dispatcher.UIThread.Post(() => ScrollViewer.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void OnCommandBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return or Key.V)) return;
        if ((DataContext as ChatViewModel)?.ComposerSession is not { } vm) return;

        // --- Ctrl+V / Cmd+V: try image paste first ---
        if (e.Key == Key.V && IsPasteModifier(e.KeyModifiers)
            && vm.Service is IChatService chatService)
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

    /// <summary>
    /// Paste is Ctrl+V on Windows/Linux and Cmd+V (Meta) on macOS. Ctrl+V is accepted on every platform.
    /// </summary>
    internal static bool IsPasteModifier(KeyModifiers modifiers)
    {
        if ((modifiers & (KeyModifiers.Shift | KeyModifiers.Alt)) != 0) return false;
        return (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
    }

    private async Task HandleClipboardPasteAsync(IChatService chatService)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        if (await TryPasteEncodedImageAsync(clipboard, chatService)) return;
        if (await TryPasteBitmapAsync(clipboard, chatService)) return;

        // No image (or service doesn't support it): fall back to pasting text.
        await FallbackTextPasteAsync();
    }

    /// <summary>
    /// Attaches PNG/JPEG bytes as they are when the clipboard exposes them under a well-known format name.
    /// </summary>
    private static async Task<bool> TryPasteEncodedImageAsync(Avalonia.Input.Platform.IClipboard clipboard,
        IChatService chatService)
    {
        try
        {
            var formats = await clipboard.GetFormatsAsync();

            var imageFormat = formats.FirstOrDefault(f =>
                string.Equals(f, "PNG", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f, "image/png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f, "image/jpeg", StringComparison.OrdinalIgnoreCase));

            if (imageFormat == null) return false;

            var raw = await clipboard.GetDataAsync(imageFormat);
            if (raw is not byte[] { Length: > 0 } bytes) return false;

            var isJpeg = imageFormat.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase);
            return chatService.TryAddImageAttachment(bytes, isJpeg ? "image/jpeg" : "image/png",
                isJpeg ? "image.jpg" : "image.png");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attaches any image the platform can decode (e.g. macOS PNG/TIFF pasteboard types), re-encoded as PNG.
    /// </summary>
    private static async Task<bool> TryPasteBitmapAsync(Avalonia.Input.Platform.IClipboard clipboard,
        IChatService chatService)
    {
        try
        {
            using var data = await clipboard.TryGetDataAsync();
            if (data == null) return false;

            using var bitmap = await data.TryGetBitmapAsync();
            if (bitmap == null) return false;

            using var stream = new MemoryStream();
            bitmap.Save(stream);
            if (stream.Length == 0) return false;

            return chatService.TryAddImageAttachment(stream.ToArray(), "image/png", "image.png");
        }
        catch
        {
            return false;
        }
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