using System.Windows.Input;
using Avalonia;
using Markdown.Avalonia;

namespace OneWare.Core.Extensions;

/// <summary>
///     Lets a template bind the command executed for clicked links, which Markdown.Avalonia only exposes on its engine.
/// </summary>
public sealed class MarkdownScrollViewerExtensions
{
    public static readonly AttachedProperty<ICommand?> HyperlinkCommandProperty =
        AvaloniaProperty.RegisterAttached<MarkdownScrollViewerExtensions, MarkdownScrollViewer, ICommand?>(
            "HyperlinkCommand");

    static MarkdownScrollViewerExtensions()
    {
        HyperlinkCommandProperty.Changed.AddClassHandler<MarkdownScrollViewer>((viewer, _) => Apply(viewer));
    }

    private MarkdownScrollViewerExtensions()
    {
    }

    public static ICommand? GetHyperlinkCommand(MarkdownScrollViewer viewer)
    {
        return viewer.GetValue(HyperlinkCommandProperty);
    }

    public static void SetHyperlinkCommand(MarkdownScrollViewer viewer, ICommand? value)
    {
        viewer.SetValue(HyperlinkCommandProperty, value);
    }

    private static void Apply(MarkdownScrollViewer viewer)
    {
        switch (viewer.Engine)
        {
            case IMarkdownEngine2 engine:
                engine.HyperlinkCommand = GetHyperlinkCommand(viewer);
                break;
            case IMarkdownEngine engine:
                engine.HyperlinkCommand = GetHyperlinkCommand(viewer);
                break;
        }
    }
}
