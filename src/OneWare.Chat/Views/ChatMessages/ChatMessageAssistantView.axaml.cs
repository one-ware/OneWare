using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Chat.ViewModels.ChatMessages;

namespace OneWare.Chat.Views.ChatMessages;

public partial class ChatMessageAssistantView : UserControl
{
    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(1.5);

    private IDisposable? _resetCopiedFeedback;

    public ChatMessageAssistantView()
    {
        InitializeComponent();
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatMessageAssistantViewModel { CanCopy: true } message) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        try
        {
            await clipboard.SetTextAsync(message.Content);
        }
        catch
        {
            // Best-effort; ignore clipboard errors.
            return;
        }

        ShowCopiedFeedback();
    }

    private void ShowCopiedFeedback()
    {
        _resetCopiedFeedback?.Dispose();

        if (Application.Current?.TryGetResource("Icon.Check", ActualThemeVariant, out var check) == true &&
            check is Geometry checkGeometry)
            CopyIcon.Data = checkGeometry;
        ToolTip.SetTip(CopyButton, "Copied");

        _resetCopiedFeedback = DispatcherTimer.RunOnce(() =>
        {
            if (Application.Current?.TryGetResource("Icon.Copy", ActualThemeVariant, out var copy) == true &&
                copy is Geometry copyGeometry)
                CopyIcon.Data = copyGeometry;
            ToolTip.SetTip(CopyButton, "Copy message (Markdown)");
        }, CopiedFeedbackDuration);
    }
}
