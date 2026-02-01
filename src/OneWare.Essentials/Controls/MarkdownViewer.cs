using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using AvaloniaEdit.Editing;

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

    public MarkdownViewer()
    {
        // We suppress this RequestBringIntoView which comes from AvaloniaEdit
        this.AddHandler(RequestBringIntoViewEvent, (sender, args) =>
        {
            if(args.Source is TextArea)
                args.Handled = true;
        }, RoutingStrategies.Bubble);
    }
}