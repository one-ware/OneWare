using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using LiveChartsCore.SkiaSharpView.Avalonia;

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