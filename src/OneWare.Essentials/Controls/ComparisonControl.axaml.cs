using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using DynamicData;
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

    private CompositeDisposable _compositeDisposable = new();

    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<ComparisonControl, string?>(nameof(Language));

    public static readonly StyledProperty<ICollection<ComparisonControlSection>?> ChunksProperty =
        AvaloniaProperty.Register<ComparisonControl, ICollection<ComparisonControlSection>?>(nameof(Chunks));

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

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _compositeDisposable.Dispose();
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

        if (_diffEditor != null)
        {
            _diffEditor.Options.AllowScrollBelowDocument = true;
            _diffEditor.Options.ConvertTabsToSpaces = true;
            _rightInfoMargin = new DiffInfoMargin();
            _diffEditor.ShowLineNumbers = true;
            _diffEditor.TextArea.LeftMargins.RemoveAt(0);
            _diffEditor.TextArea.LeftMargins.Insert(0, _rightInfoMargin);
            _rightBackgroundRenderer = new DiffLineBackgroundRenderer();
            _diffEditor.TextArea.TextView.BackgroundRenderers.Add(_rightBackgroundRenderer);
            _diffEditor.TextArea.TextView.ScrollOffsetChanged += OnDiffScrollOffsetChanged;
        }

        if (_headEditor != null)
        {
            _headEditor.Options.AllowScrollBelowDocument = true;
            _headEditor.Options.ConvertTabsToSpaces = true;
            _headEditor.ShowLineNumbers = true;
            _leftInfoMargin = new DiffInfoMargin();
            _headEditor.TextArea.LeftMargins.RemoveAt(0);
            _headEditor.TextArea.LeftMargins.Insert(0, _leftInfoMargin);
            _leftBackgroundRenderer = new DiffLineBackgroundRenderer();
            _headEditor.TextArea.TextView.BackgroundRenderers.Add(_leftBackgroundRenderer);
            _headEditor.TextArea.TextView.ScrollOffsetChanged += OnHeadScrollOffsetChanged;
        }
        
        ApplyLanguage();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChunksProperty)
        {
            if (Chunks != null)
            {
                if (Chunks is INotifyCollectionChanged observableCollection)
                {
                    Observable.FromEventPattern(observableCollection, nameof(observableCollection.CollectionChanged))
                        .Throttle(TimeSpan.FromSeconds(1))
                        .ObserveOn(AvaloniaScheduler.Instance)
                        .Subscribe(_ => ApplyChunks())
                        .DisposeWith(_compositeDisposable);
                }
                ApplyChunks();
            }
        }
    }

    private void DetachTemplateHandlers()
    {
        if (_diffEditor != null)
        {
            _diffEditor.TextArea.TextView.ScrollOffsetChanged -= OnDiffScrollOffsetChanged;
        }

        if (_headEditor != null)
        {
            _headEditor.TextArea.TextView.ScrollOffsetChanged -= OnHeadScrollOffsetChanged;
        }
    }

    private void OnDiffScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (_diffEditor == null || _headEditor == null) return;
        _headEditor.ScrollViewer.Offset = _diffEditor.ScrollViewer.Offset;
    }

    private void OnHeadScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (_diffEditor == null || _headEditor == null) return;
        _diffEditor.ScrollViewer.Offset = _headEditor.ScrollViewer.Offset;
    }

    private void ApplyLanguage()
    {
        _compositeDisposable.Dispose();
        _compositeDisposable = new CompositeDisposable();

        if (_diffEditor == null || _headEditor == null) return;

        //Syntax Highlighting
        var languageManager = ContainerLocator.Container.Resolve<ILanguageManager>();
        if (languageManager.GetTextMateScopeByExtension(Language ?? "") is { } scope)
        {
            var textMateDiff = _diffEditor.InstallTextMate(languageManager.RegistryOptions);
            textMateDiff.SetGrammar(scope);
            var textMateHead = _headEditor.InstallTextMate(languageManager.RegistryOptions);
            textMateHead.SetGrammar(scope);

            textMateDiff.DisposeWith(_compositeDisposable);
            textMateHead.DisposeWith(_compositeDisposable);

            languageManager.WhenValueChanged(x => x.CurrentEditorTheme).Subscribe(x =>
            {
                textMateDiff.SetTheme(x);
                textMateHead.SetTheme(x);
            }).DisposeWith(_compositeDisposable);
        }
    }

    private void ApplyChunks()
    {
        if (Chunks == null) throw new NullReferenceException(nameof(Chunks));

        if (Chunks.Count == 0)
        {
            return;
        }

        var leftScrollInfo = Chunks.Select(x => x.LeftDiff)
            .Aggregate((x, y) => x.Concat(y).ToList()).Select(x => new ScrollInfoLine(x.LineNumber, x.Style switch
            {
                DiffContext.Added => AddBrush,
                DiffContext.Deleted => DeleteBrush,
                _ => Brushes.Transparent
            })).ToArray();

        var rightScrollInfo = Chunks.Select(x => x.RightDiff)
            .Aggregate((x, y) => x.Concat(y).ToList()).Select(x => new ScrollInfoLine(x.LineNumber, x.Style switch
            {
                DiffContext.Added => AddBrush,
                DiffContext.Deleted => DeleteBrush,
                _ => Brushes.Transparent
            })).ToArray();

        ScrollInfoLeft.Refresh("Comparison", leftScrollInfo);
        ScrollInfoRight.Refresh("Comparison", rightScrollInfo);

        if (_diffEditor == null || _headEditor == null || _scrollLeft == null || _scrollRight == null ||
            _leftSide == null || _rightSide == null || _leftInfoMargin == null || _rightInfoMargin == null ||
            _leftBackgroundRenderer == null || _rightBackgroundRenderer == null)
            return;

        if (Chunks.Count > 1)
        {
            _diffEditor.IsVisible = false;
            _headEditor.IsVisible = false;
            _scrollLeft.IsVisible = true;
            _scrollRight.IsVisible = true;
            _leftSide.Children.Clear();
            _leftSide.RowDefinitions.Clear();
            _rightSide.Children.Clear();
            _rightSide.RowDefinitions.Clear();

            foreach (var chunk in Chunks)
            {
                var row = Chunks.IndexOf(chunk);
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

                // draw left diff
                var leftMargin = new DiffInfoMargin { Lines = chunk.LeftDiff };
                var left = new TextEditor();
                //left.SyntaxHighlighting = EditorThemeManager.Instance.SelectedTheme.Theme;
                left.ShowLineNumbers = true;
                left.TextArea.LeftMargins.RemoveAt(0);
                left.TextArea.LeftMargins.Insert(0, leftMargin);
                var leftBackgroundRenderer = new DiffLineBackgroundRenderer { Lines = chunk.LeftDiff };
                left.TextArea.TextView.BackgroundRenderers.Add(leftBackgroundRenderer);
                left.Text = string.Join("\n", chunk.LeftDiff.Select(x => x.Text)).Replace("\t", "    ");

                Grid.SetRow(left, 2 * row + 1);
                _leftSide.Children.Add(left);

                // draw right diff
                var rightMargin = new DiffInfoMargin { Lines = chunk.RightDiff };
                var right = new TextEditor();
                //right.SyntaxHighlighting = EditorThemeManager.Instance.SelectedTheme.Theme;
                right.ShowLineNumbers = true;
                right.TextArea.LeftMargins.RemoveAt(0);
                right.TextArea.LeftMargins.Insert(0, rightMargin);
                var rightBackgroundRenderer = new DiffLineBackgroundRenderer { Lines = chunk.RightDiff };
                right.TextArea.TextView.BackgroundRenderers.Add(rightBackgroundRenderer);
                right.Text = string.Join("\n", chunk.RightDiff.Select(x => x.Text)).Replace("\t", "    ");
                ;

                Grid.SetRow(right, 2 * row + 1);
                _rightSide.Children.Add(right);
            }
        }
        else if (Chunks.Count == 1)
        {
            _diffEditor.IsVisible = true;
            _headEditor.IsVisible = true;
            _scrollLeft.IsVisible = false;
            _scrollRight.IsVisible = false;

            var chunk = Chunks.First();

            _leftInfoMargin.Lines = chunk.LeftDiff;
            _leftBackgroundRenderer.Lines = chunk.LeftDiff;
            _headEditor.Text = string.Join("\n", chunk.LeftDiff.Select(x => x.Text)).Replace("\t", "    ");

            _rightInfoMargin.Lines = chunk.RightDiff;
            _rightBackgroundRenderer.Lines = chunk.RightDiff;
            _diffEditor.Text = string.Join("\n", chunk.RightDiff.Select(x => x.Text)).Replace("\t", "    ");
        }
    }
}