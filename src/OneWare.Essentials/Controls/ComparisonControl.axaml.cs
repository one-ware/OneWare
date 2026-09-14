using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using DynamicData.Binding;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.EditorExtensions;

namespace OneWare.Essentials.Controls;

public class ComparisonControlSection
{
    public string? DiffSectionHeader { get; init; }
    public required List<DiffLineModel> LeftDiff { get; init; }
    public required List<DiffLineModel> RightDiff { get; init; }
}

public class ComparisonControl : TemplatedControl
{
    private const int MaxScrollToLineRetries = 10;

    public static IBrush AddBrush { get; } = new SolidColorBrush(Color.FromArgb(150, 150, 200, 100));
    public static IBrush DeleteBrush { get; } = new SolidColorBrush(Color.FromArgb(150, 175, 50, 50));

    private TextEditor? _diffEditor;
    private TextEditor? _headEditor;
    private ScrollViewer? _scrollLeft;
    private ScrollViewer? _scrollRight;
    private Grid? _leftSide;
    private Grid? _rightSide;

    private DiffLineBackgroundRenderer? _leftBackgroundRenderer;
    private DiffLineBackgroundRenderer? _rightBackgroundRenderer;
    private DiffInfoMargin? _leftInfoMargin;
    private DiffInfoMargin? _rightInfoMargin;

    /// <summary>
    /// Editors created for the multi chunk layout. They are recreated whenever the chunks change and
    /// need to be re-registered with TextMate when the language changes.
    /// </summary>
    private readonly List<TextEditor> _chunkEditors = new();

    /// <summary>
    /// TextMate installations per editor. Installing TextMate twice on the same editor reuses the
    /// coloring transformer that the previous installation disposed but left in the line transformers,
    /// which silently kills highlighting. Every editor is therefore installed exactly once and its
    /// installation is kept alive for as long as the editor exists.
    /// </summary>
    private readonly Dictionary<TextEditor, TextMate.Installation> _textMateInstallations = new();

    private readonly SerialDisposable _themeDisposable = new();

    private readonly SerialDisposable _chunksDisposable = new();

    private bool _handlersAttached;
    private bool _isSyncingScroll;
    private Vector _lastLeftOffset;
    private Vector _lastRightOffset;
    private bool _scrollToLinePending;
    private int _scrollToLineRetries;

    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<ComparisonControl, string?>(nameof(Language));

    public static readonly StyledProperty<ICollection<ComparisonControlSection>?> ChunksProperty =
        AvaloniaProperty.Register<ComparisonControl, ICollection<ComparisonControlSection>?>(nameof(Chunks));

    /// <summary>
    /// One-based line of the rendered diff to scroll into view. Zero means no scroll target.
    /// </summary>
    public static readonly StyledProperty<int> ScrollToLineProperty =
        AvaloniaProperty.Register<ComparisonControl, int>(nameof(ScrollToLine));

    public static readonly DirectProperty<ComparisonControl, ScrollInfoContext> ScrollInfoLeftProperty =
        AvaloniaProperty.RegisterDirect<ComparisonControl, ScrollInfoContext>(
            nameof(ScrollInfoLeft),
            o => o.ScrollInfoLeft);

    public static readonly DirectProperty<ComparisonControl, ScrollInfoContext> ScrollInfoRightProperty =
        AvaloniaProperty.RegisterDirect<ComparisonControl, ScrollInfoContext>(
            nameof(ScrollInfoRight),
            o => o.ScrollInfoRight);

    public ScrollInfoContext ScrollInfoLeft
    {
        get;
        set => SetAndRaise(ScrollInfoLeftProperty, ref field, value);
    } = new();

    public ScrollInfoContext ScrollInfoRight
    {
        get;
        set => SetAndRaise(ScrollInfoRightProperty, ref field, value);
    } = new();

    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public ICollection<ComparisonControlSection>? Chunks
    {
        get => GetValue(ChunksProperty);
        set => SetValue(ChunksProperty, value);
    }

    public int ScrollToLine
    {
        get => GetValue(ScrollToLineProperty);
        set => SetValue(ScrollToLineProperty, value);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // The control is detached and re-attached when its dock tab is switched. Everything that was
        // released on detach has to be restored here, because the template is not applied again.
        AttachTemplateHandlers();
        SubscribeToChunks();
        ApplyLanguage();
        ApplyChunks();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _chunksDisposable.Disposable = null;
        _themeDisposable.Disposable = null;
        DetachTemplateHandlers();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachTemplateHandlers();

        _diffEditor = e.NameScope.Find<TextEditor>("DiffEditor");
        _headEditor = e.NameScope.Find<TextEditor>("HeadEditor");
        _scrollLeft = e.NameScope.Find<ScrollViewer>("ScrollLeft");
        _scrollRight = e.NameScope.Find<ScrollViewer>("ScrollRight");
        _leftSide = e.NameScope.Find<Grid>("LeftSide");
        _rightSide = e.NameScope.Find<Grid>("RightSide");

        // A rebuilt template brings new editors, the installations of the old ones have to be released.
        DisposeOrphanedTextMateInstallations();

        if (_diffEditor != null)
        {
            ConfigureEditor(_diffEditor);
            _rightInfoMargin = new DiffInfoMargin();
            _diffEditor.TextArea.LeftMargins.RemoveAt(0);
            _diffEditor.TextArea.LeftMargins.Insert(0, _rightInfoMargin);
            _rightBackgroundRenderer = new DiffLineBackgroundRenderer();
            _diffEditor.TextArea.TextView.BackgroundRenderers.Add(_rightBackgroundRenderer);
        }

        if (_headEditor != null)
        {
            ConfigureEditor(_headEditor);
            _leftInfoMargin = new DiffInfoMargin();
            _headEditor.TextArea.LeftMargins.RemoveAt(0);
            _headEditor.TextArea.LeftMargins.Insert(0, _leftInfoMargin);
            _leftBackgroundRenderer = new DiffLineBackgroundRenderer();
            _headEditor.TextArea.TextView.BackgroundRenderers.Add(_leftBackgroundRenderer);
        }

        AttachTemplateHandlers();
        ApplyLanguage();
        ApplyChunks();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChunksProperty)
        {
            SubscribeToChunks();
            ApplyChunks();
        }
        else if (change.Property == LanguageProperty)
        {
            // The language binding often resolves after the template has been applied, so the grammar
            // has to be installed here as well.
            ApplyLanguage();
        }
        else if (change.Property == ScrollToLineProperty)
        {
            _scrollToLineRetries = 0;
            ScrollToTargetLine();
        }
    }

    private void DisposeOrphanedTextMateInstallations()
    {
        foreach (var editor in _textMateInstallations.Keys.ToArray())
        {
            if (editor == _diffEditor || editor == _headEditor || _chunkEditors.Contains(editor)) continue;
            if (_textMateInstallations.Remove(editor, out var installation)) installation.Dispose();
        }
    }

    private static void ConfigureEditor(TextEditor editor)
    {
        editor.Options.AllowScrollBelowDocument = true;
        editor.Options.ConvertTabsToSpaces = true;
        editor.ShowLineNumbers = true;
    }

    private void SubscribeToChunks()
    {
        _chunksDisposable.Disposable = null;

        if (Chunks is INotifyCollectionChanged observableCollection)
        {
            _chunksDisposable.Disposable = Observable
                .FromEventPattern(observableCollection, nameof(observableCollection.CollectionChanged))
                .Throttle(TimeSpan.FromSeconds(1))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(_ => ApplyChunks());
        }
    }

    private void AttachTemplateHandlers()
    {
        if (_handlersAttached) return;
        if (_diffEditor == null || _headEditor == null) return;

        _diffEditor.TextArea.TextView.ScrollOffsetChanged += OnDiffScrollOffsetChanged;
        _headEditor.TextArea.TextView.ScrollOffsetChanged += OnHeadScrollOffsetChanged;

        if (_scrollLeft != null) _scrollLeft.ScrollChanged += OnLeftScrollChanged;
        if (_scrollRight != null) _scrollRight.ScrollChanged += OnRightScrollChanged;

        _handlersAttached = true;
    }

    private void DetachTemplateHandlers()
    {
        if (!_handlersAttached) return;

        if (_diffEditor != null) _diffEditor.TextArea.TextView.ScrollOffsetChanged -= OnDiffScrollOffsetChanged;
        if (_headEditor != null) _headEditor.TextArea.TextView.ScrollOffsetChanged -= OnHeadScrollOffsetChanged;
        if (_scrollLeft != null) _scrollLeft.ScrollChanged -= OnLeftScrollChanged;
        if (_scrollRight != null) _scrollRight.ScrollChanged -= OnRightScrollChanged;

        _handlersAttached = false;
    }

    private void OnDiffScrollOffsetChanged(object? sender, EventArgs e)
    {
        SyncScroll(_diffEditor?.ScrollViewer, _headEditor?.ScrollViewer);
    }

    private void OnHeadScrollOffsetChanged(object? sender, EventArgs e)
    {
        SyncScroll(_headEditor?.ScrollViewer, _diffEditor?.ScrollViewer);
    }

    private void OnLeftScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        SyncScrollViewers(_scrollLeft, _scrollRight, ref _lastLeftOffset, ref _lastRightOffset);
    }

    private void OnRightScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        SyncScrollViewers(_scrollRight, _scrollLeft, ref _lastRightOffset, ref _lastLeftOffset);
    }

    /// <summary>
    /// Synchronises the two scroll viewers of the multi chunk layout. <see cref="ScrollViewer.ScrollChanged" />
    /// is raised from the layout pass and not from the offset setter, so a simple re-entrancy flag does not
    /// see the echo. Instead the offset that was last written to or observed on each side is remembered and
    /// the echo is recognised by comparing against it.
    /// </summary>
    private void SyncScrollViewers(ScrollViewer? source, ScrollViewer? target, ref Vector lastSourceOffset,
        ref Vector lastTargetOffset)
    {
        if (source == null || target == null) return;
        if (IsSameOffset(source.Offset, lastSourceOffset)) return;

        lastSourceOffset = source.Offset;

        var offset = ClampToTarget(source.Offset, target);
        if (IsSameOffset(target.Offset, offset))
        {
            lastTargetOffset = target.Offset;
            return;
        }

        lastTargetOffset = offset;
        target.Offset = offset;
    }

    private static Vector ClampToTarget(Vector offset, ScrollViewer target)
    {
        var maxX = Math.Max(0, target.Extent.Width - target.Viewport.Width);
        var maxY = Math.Max(0, target.Extent.Height - target.Viewport.Height);
        return new Vector(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }

    private static bool IsSameOffset(Vector a, Vector b)
    {
        return Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5;
    }

    /// <summary>
    /// Copies the scroll offset from one side to the other. The target offset is clamped to the range
    /// the target can actually reach, otherwise the target clamps it itself, reports the clamped value
    /// back and drags the source out of position again.
    /// </summary>
    private void SyncScroll(ScrollViewer? source, ScrollViewer? target)
    {
        if (source == null || target == null || _isSyncingScroll) return;

        var offset = ClampToTarget(source.Offset, target);
        if (IsSameOffset(target.Offset, offset)) return;

        _isSyncingScroll = true;
        try
        {
            target.Offset = offset;
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void ApplyLanguage()
    {
        if (_diffEditor == null || _headEditor == null) return;

        //Syntax Highlighting
        if (Design.IsDesignMode || ContainerLocator.Container == null) return;

        var languageManager = ContainerLocator.Container.Resolve<ILanguageManager>();
        if (languageManager.GetTextMateScopeByExtension(Language ?? "") is not { } scope) return;

        foreach (var editor in new[] { _diffEditor, _headEditor }.Concat(_chunkEditors))
        {
            if (!_textMateInstallations.TryGetValue(editor, out var installation))
            {
                installation = editor.InstallTextMate(languageManager.RegistryOptions);
                installation.SetTheme(languageManager.CurrentEditorTheme);
                _textMateInstallations[editor] = installation;
            }

            installation.SetGrammar(scope);
        }

        _themeDisposable.Disposable ??= languageManager.WhenValueChanged(x => x.CurrentEditorTheme).Subscribe(x =>
        {
            foreach (var installation in _textMateInstallations.Values) installation.SetTheme(x);
        });
    }

    private void ApplyChunks()
    {
        var chunks = NormalizeChunks(Chunks);

        if (_diffEditor == null || _headEditor == null || _scrollLeft == null || _scrollRight == null ||
            _leftSide == null || _rightSide == null || _leftInfoMargin == null || _rightInfoMargin == null ||
            _leftBackgroundRenderer == null || _rightBackgroundRenderer == null)
            return;

        ScrollInfoLeft.Refresh("Comparison", BuildScrollInfo(chunks, true));
        ScrollInfoRight.Refresh("Comparison", BuildScrollInfo(chunks, false));

        ClearChunkEditors();

        if (chunks.Count > 1)
        {
            _diffEditor.IsVisible = false;
            _headEditor.IsVisible = false;
            _scrollLeft.IsVisible = true;
            _scrollRight.IsVisible = true;

            for (var row = 0; row < chunks.Count; row++)
            {
                var chunk = chunks[row];

                // draw header
                var textBlockLeft = new TextBlock
                {
                    Text = "HEAD: " + chunk.DiffSectionHeader
                };

                var textBlockRight = new TextBlock
                {
                    Text = "LOCAL: " + chunk.DiffSectionHeader
                };

                _leftSide.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
                _leftSide.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                _rightSide.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
                _rightSide.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetRow(textBlockLeft, 2 * row);
                Grid.SetRow(textBlockRight, 2 * row);
                _leftSide.Children.Add(textBlockLeft);
                _rightSide.Children.Add(textBlockRight);

                var left = CreateChunkEditor(chunk.LeftDiff);
                Grid.SetRow(left, 2 * row + 1);
                _leftSide.Children.Add(left);

                var right = CreateChunkEditor(chunk.RightDiff);
                Grid.SetRow(right, 2 * row + 1);
                _rightSide.Children.Add(right);
            }

            // The editors were recreated, so they need their grammar installed again.
            ApplyLanguage();
        }
        else
        {
            _diffEditor.IsVisible = true;
            _headEditor.IsVisible = true;
            _scrollLeft.IsVisible = false;
            _scrollRight.IsVisible = false;

            var leftDiff = chunks.Count == 1 ? chunks[0].LeftDiff : new List<DiffLineModel>();
            var rightDiff = chunks.Count == 1 ? chunks[0].RightDiff : new List<DiffLineModel>();

            _leftInfoMargin.Lines = leftDiff;
            _leftBackgroundRenderer.Lines = leftDiff;
            _headEditor.Text = BuildText(leftDiff);

            _rightInfoMargin.Lines = rightDiff;
            _rightBackgroundRenderer.Lines = rightDiff;
            _diffEditor.Text = BuildText(rightDiff);

            ScrollToTargetLine();
        }
    }

    private TextEditor CreateChunkEditor(List<DiffLineModel> lines)
    {
        var editor = new TextEditor();
        ConfigureEditor(editor);
        editor.TextArea.LeftMargins.RemoveAt(0);
        editor.TextArea.LeftMargins.Insert(0, new DiffInfoMargin { Lines = lines });
        editor.TextArea.TextView.BackgroundRenderers.Add(new DiffLineBackgroundRenderer { Lines = lines });
        editor.Text = BuildText(lines);
        _chunkEditors.Add(editor);
        return editor;
    }

    private void ClearChunkEditors()
    {
        foreach (var editor in _chunkEditors)
        {
            // The editor is discarded, so its installation (and the tokenizer thread behind it) has to go.
            if (_textMateInstallations.Remove(editor, out var installation)) installation.Dispose();
        }

        _chunkEditors.Clear();
        _leftSide?.Children.Clear();
        _leftSide?.RowDefinitions.Clear();
        _rightSide?.Children.Clear();
        _rightSide?.RowDefinitions.Clear();
    }

    private static string BuildText(List<DiffLineModel> lines)
    {
        return string.Join("\n", lines.Select(x => x.Text)).Replace("\t", "    ");
    }

    private static ScrollInfoLine[] BuildScrollInfo(IReadOnlyList<ComparisonControlSection> chunks, bool left)
    {
        // The rendered document line is the index within the diff, not DiffLineModel.LineNumber, which
        // refers to the original file and is -1 for the blank filler lines.
        var infoLines = new List<ScrollInfoLine>();
        var offset = 0;

        foreach (var chunk in chunks)
        {
            var diff = left ? chunk.LeftDiff : chunk.RightDiff;

            for (var i = 0; i < diff.Count; i++)
            {
                switch (diff[i].Style)
                {
                    case DiffContext.Added:
                        infoLines.Add(new ScrollInfoLine(offset + i + 1, AddBrush));
                        break;
                    case DiffContext.Deleted:
                        infoLines.Add(new ScrollInfoLine(offset + i + 1, DeleteBrush));
                        break;
                }
            }

            offset += diff.Count;
        }

        return infoLines.ToArray();
    }

    /// <summary>
    /// Pads both sides of every chunk to the same line count. Producers do not guarantee this (the Git
    /// patch parser can stop padding early), and different line counts give the two editors different
    /// scroll extents, which breaks both the row alignment and the scroll synchronisation.
    /// </summary>
    private static IReadOnlyList<ComparisonControlSection> NormalizeChunks(
        ICollection<ComparisonControlSection>? chunks)
    {
        if (chunks == null || chunks.Count == 0) return Array.Empty<ComparisonControlSection>();

        var normalized = new List<ComparisonControlSection>(chunks.Count);

        foreach (var chunk in chunks)
        {
            var lineCount = Math.Max(chunk.LeftDiff.Count, chunk.RightDiff.Count);

            if (chunk.LeftDiff.Count == lineCount && chunk.RightDiff.Count == lineCount)
            {
                normalized.Add(chunk);
                continue;
            }

            normalized.Add(new ComparisonControlSection
            {
                DiffSectionHeader = chunk.DiffSectionHeader,
                LeftDiff = Pad(chunk.LeftDiff, lineCount),
                RightDiff = Pad(chunk.RightDiff, lineCount)
            });
        }

        return normalized;

        static List<DiffLineModel> Pad(List<DiffLineModel> lines, int lineCount)
        {
            if (lines.Count == lineCount) return lines;

            var padded = new List<DiffLineModel>(lines);
            while (padded.Count < lineCount) padded.Add(DiffLineModel.CreateBlank());
            return padded;
        }
    }

    /// <summary>
    /// Scrolls the diff editor to <see cref="ScrollToLine" />. The head editor follows through the
    /// existing scroll offset synchronisation. Only applies to the single chunk layout, the multi chunk
    /// layout renders one editor per chunk and has no single line coordinate space.
    /// </summary>
    private void ScrollToTargetLine()
    {
        if (ScrollToLine <= 0) return;
        if (_diffEditor is not { IsVisible: true }) return;
        if (_scrollToLinePending) return;

        _scrollToLinePending = true;

        Dispatcher.UIThread.Post(() =>
        {
            _scrollToLinePending = false;

            var line = ScrollToLine;
            if (line <= 0) return;
            if (_diffEditor is not { IsVisible: true, Document: { } document }) return;

            var textView = _diffEditor.TextArea.TextView;

            // Without a valid layout the scroll request is silently dropped, so retry once the editor
            // has been measured.
            if (textView.Bounds.Height <= 0)
            {
                if (_scrollToLineRetries++ >= MaxScrollToLineRetries) return;
                Dispatcher.UIThread.Post(ScrollToTargetLine, DispatcherPriority.Background);
                return;
            }

            _scrollToLineRetries = 0;
            textView.EnsureVisualLines();
            _diffEditor.ScrollToLine(Math.Clamp(line, 1, Math.Max(1, document.LineCount)));
        }, DispatcherPriority.Background);
    }
}
