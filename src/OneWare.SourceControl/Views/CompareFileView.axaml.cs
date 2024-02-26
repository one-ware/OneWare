using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using DynamicData.Binding;
using OneWare.Essentials.Services;
using OneWare.SourceControl.EditorExtensions;
using OneWare.SourceControl.ViewModels;
using Prism.Ioc;

namespace OneWare.SourceControl.Views
{
    public partial class CompareFileView : UserControl
    {
        private readonly DiffLineBackgroundRenderer _leftBackgroundRenderer, _rightBackgroundRenderer;
        private readonly DiffInfoMargin _leftInfoMargin, _rightInfoMargin;

        private CompositeDisposable _compositeDisposable = new();
        
        public CompareFileView()
        {
            InitializeComponent();
            
            DiffEditor.Options.AllowScrollBelowDocument = true;
            DiffEditor.Options.ConvertTabsToSpaces = true;
            _rightInfoMargin = new DiffInfoMargin();
            DiffEditor.ShowLineNumbers = true;
            DiffEditor.TextArea.LeftMargins.RemoveAt(0);
            DiffEditor.TextArea.LeftMargins.Insert(0, _rightInfoMargin);
            _rightBackgroundRenderer = new DiffLineBackgroundRenderer();
            DiffEditor.TextArea.TextView.BackgroundRenderers.Add(_rightBackgroundRenderer);

            HeadEditor.Options.AllowScrollBelowDocument = true;
            HeadEditor.Options.ConvertTabsToSpaces = true;
            HeadEditor.ShowLineNumbers = true;
            _leftInfoMargin = new DiffInfoMargin();
            HeadEditor.TextArea.LeftMargins.RemoveAt(0);
            HeadEditor.TextArea.LeftMargins.Insert(0, _leftInfoMargin);
            _leftBackgroundRenderer = new DiffLineBackgroundRenderer();
            HeadEditor.TextArea.TextView.BackgroundRenderers.Add(_leftBackgroundRenderer);

            DiffEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) =>
            {
                HeadEditor.ScrollViewer.Offset = DiffEditor.ScrollViewer.Offset;
                // var canScrollH = DiffEditor.ScrollViewer.Ca .GetValue(ScrollViewer.CanHorizontallyScrollProperty);
                // if (canScrollH) HeadEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                // else HeadEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            };

            HeadEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) =>
            {
                DiffEditor.ScrollViewer.Offset = HeadEditor.ScrollViewer.Offset;
                // var canScrollH = HeadEditor.ScrollViewer.GetValue(ScrollViewer.CanHorizontallyScrollProperty);
                // if (canScrollH) DiffEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                // else DiffEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            };

            this.DataContextChanged += (_, _) =>
            {
                _compositeDisposable.Dispose();
                _compositeDisposable = new CompositeDisposable();
                
                if (DataContext is not CompareFileViewModel vm) return;
                
                //Syntax Highlighting
                var language = Path.GetExtension(Path.GetExtension(vm.FullPath));
            
                var languageManager = ContainerLocator.Container.Resolve<ILanguageManager>();
                if (languageManager.GetTextMateScopeByExtension(language) is {} scope)
                {
                    var textMateDiff = DiffEditor.InstallTextMate(languageManager.RegistryOptions);
                    textMateDiff.SetGrammar(scope);
                    var textMateHead = HeadEditor.InstallTextMate(languageManager.RegistryOptions);
                    textMateHead.SetGrammar(scope);

                    textMateDiff.DisposeWith(_compositeDisposable);
                    textMateHead.DisposeWith(_compositeDisposable);

                    languageManager.WhenValueChanged(x => x.CurrentEditorTheme).Subscribe(x =>
                    {
                        textMateDiff.SetTheme(x);
                        textMateHead.SetTheme(x);
                    }).DisposeWith(_compositeDisposable);
                }
                
                vm.WhenValueChanged(a => a.Chunks)
                    .Subscribe(b =>
                    {
                        if (b != null) Load(vm);
                    }).DisposeWith(_compositeDisposable);
            };
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            _compositeDisposable.Dispose();
        }

        private void Load(CompareFileViewModel vm)
        {
            if (vm.Chunks == null) throw new NullReferenceException(nameof(vm.Chunks));

            if (vm.Chunks.Count > 1)
            {
                DiffEditor.IsVisible = false;
                HeadEditor.IsVisible = false;
                ScrollLeft.IsVisible = true;
                ScrollRight.IsVisible = true;
                LeftSide.Children.Clear();
                RightSide.Children.Clear();

                foreach (var chunk in vm.Chunks)
                {
                    var row = vm.Chunks.IndexOf(chunk);
                    // draw header
                    var textBlockLeft = new TextBlock
                    {
                        Text = "HEAD: " + chunk.DiffSectionHeader
                    };

                    var textBlockRight = new TextBlock
                    {
                        Text = "LOCAL: " + chunk.DiffSectionHeader
                    };

                    LeftSide.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
                    LeftSide.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    RightSide.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
                    RightSide.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    Grid.SetRow(textBlockLeft, 2 * row);
                    Grid.SetRow(textBlockRight, 2 * row);
                    LeftSide.Children.Add(textBlockLeft);
                    RightSide.Children.Add(textBlockRight);

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
                    LeftSide.Children.Add(left);

                    // draw right diff
                    var rightMargin = new DiffInfoMargin { Lines = chunk.RightDiff };
                    var right = new TextEditor();
                    //right.SyntaxHighlighting = EditorThemeManager.Instance.SelectedTheme.Theme;
                    right.ShowLineNumbers = true;
                    right.TextArea.LeftMargins.RemoveAt(0);
                    right.TextArea.LeftMargins.Insert(0, rightMargin);
                    var rightBackgroundRenderer = new DiffLineBackgroundRenderer { Lines = chunk.RightDiff };
                    right.TextArea.TextView.BackgroundRenderers.Add(rightBackgroundRenderer);
                    right.Text = string.Join("\n", chunk.RightDiff.Select(x => x.Text)).Replace("\t", "    ");;

                    Grid.SetRow(right, 2 * row + 1);
                    RightSide.Children.Add(right);
                }
            }
            else if (vm.Chunks.Count == 1)
            {
                DiffEditor.IsVisible = true;
                HeadEditor.IsVisible = true;
                ScrollLeft.IsVisible = false;
                ScrollRight.IsVisible = false;

                var chunk = vm.Chunks[0];

                _leftInfoMargin.Lines = chunk.LeftDiff;
                _leftBackgroundRenderer.Lines = chunk.LeftDiff;
                HeadEditor.Text = string.Join("\n", chunk.LeftDiff.Select(x => x.Text)).Replace("\t", "    ");;

                _rightInfoMargin.Lines = chunk.RightDiff;
                _rightBackgroundRenderer.Lines = chunk.RightDiff;
                DiffEditor.Text = string.Join("\n", chunk.RightDiff.Select(x => x.Text)).Replace("\t", "    ");;
            }
        }
    }
}