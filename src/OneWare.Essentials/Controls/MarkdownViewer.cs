using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;

namespace OneWare.Essentials.Controls;

public class MarkdownViewer : TemplatedControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    public static readonly StyledProperty<bool> AutoScrollToBottomProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(AutoScrollToBottom));

    private Control? _markdownScrollViewer;
    private ScrollViewer? _scrollViewer;

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public bool AutoScrollToBottom
    {
        get => GetValue(AutoScrollToBottomProperty);
        set => SetValue(AutoScrollToBottomProperty, value);
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _markdownScrollViewer = e.NameScope.Find<Control>("PART_MarkdownScrollViewer");
        _scrollViewer = null;
        RequestScrollToBottom();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty || change.Property == AutoScrollToBottomProperty)
            RequestScrollToBottom();
    }

    private void RequestScrollToBottom()
    {
        if (!AutoScrollToBottom) return;

        var viewer = GetScrollViewer();
        if (viewer == null) return;

        var maxOffset = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
        viewer.Offset = new Vector(viewer.Offset.X, maxOffset);
    }

    private ScrollViewer? GetScrollViewer()
    {
        if (_scrollViewer != null) return _scrollViewer;
        if (_markdownScrollViewer == null) return null;

        _scrollViewer = _markdownScrollViewer.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        return _scrollViewer;
    }
}
