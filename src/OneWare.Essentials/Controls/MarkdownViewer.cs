using Avalonia;
using Avalonia.Controls.Primitives;

namespace OneWare.Essentials.Controls;

public class MarkdownViewer : TemplatedControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }
}