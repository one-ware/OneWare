using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Controls;

public class MarkdownViewer : TemplatedControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    public static readonly StyledProperty<bool> SelectionEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(SelectionEnabled));
    
    public static readonly StyledProperty<bool> VirtualizationEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(VirtualizationEnabled));
    
    public static readonly StyledProperty<double> VirtualizationCacheLengthProperty =
        AvaloniaProperty.Register<MarkdownViewer, double>(nameof(VirtualizationCacheLength));
    
    public static readonly StyledProperty<bool> AutoScrollToBottomProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(AutoScrollToBottom));

    public static readonly StyledProperty<string?> LinkBasePathProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(LinkBasePath));

    public static readonly DirectProperty<MarkdownViewer, ICommand> LinkCommandProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, ICommand>(nameof(LinkCommand), x => x.LinkCommand);

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
    
    public bool SelectionEnabled
    {
        get => GetValue(SelectionEnabledProperty);
        set => SetValue(SelectionEnabledProperty, value);
    }
    
    public bool VirtualizationEnabled
    {
        get => GetValue(VirtualizationEnabledProperty);
        set => SetValue(VirtualizationEnabledProperty, value);
    }

    public double VirtualizationCacheLength
    {
        get => GetValue(VirtualizationCacheLengthProperty);
        set => SetValue(VirtualizationCacheLengthProperty, value);
    }

    /// <summary>
    ///     Folder (or a file inside it) that relative file links are resolved against before the active project and the
    ///     projects folder.
    /// </summary>
    public string? LinkBasePath
    {
        get => GetValue(LinkBasePathProperty);
        set => SetValue(LinkBasePathProperty, value);
    }

    /// <summary>
    ///     Executed with the target of a clicked link. Files open in the IDE (a <c>:line</c> or <c>#L</c> suffix jumps
    ///     to the line), folders in the file manager and anything else in the browser.
    /// </summary>
    public ICommand LinkCommand { get; }

    public MarkdownViewer()
    {
        LinkCommand = new AsyncRelayCommand<string?>(OpenLinkAsync);

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

    private async Task OpenLinkAsync(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || link.TrimStart().StartsWith('#')) return;

        if (LocalLinkResolver.IsExternalLink(link))
        {
            PlatformHelper.OpenHyperLink(link.Trim());
            return;
        }

        var services = ContainerLocator.Container;
        string?[] baseDirectories =
        [
            File.Exists(LinkBasePath) ? Path.GetDirectoryName(LinkBasePath) : LinkBasePath,
            services?.Resolve<IProjectExplorerService>().ActiveProject?.RootFolderPath,
            services?.Resolve<IPaths>().ProjectsDirectory
        ];

        if (!LocalLinkResolver.TryResolve(link, baseDirectories, out var path, out var line))
        {
            services?.Resolve<ILogger>().Warning($"Could not find {link}");
            return;
        }

        if (Directory.Exists(path) || services == null)
        {
            PlatformHelper.OpenExplorerPath(path);
            return;
        }

        var document = await services.Resolve<IMainDockService>().OpenFileAsync(path);
        if (line is > 0 && document is IEditor editor)
            editor.JumpToLine(Math.Min(line.Value, editor.CurrentDocument.LineCount));
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
