using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using TextMateSharp.Registry;

namespace OneWare.Essentials.EditorExtensions;

public class ExtendedTextEditor : TextEditor
{
    public ExtendedTextEditor()
    {
        // //Avoid Styles to improve performance
        Bind(FontFamilyProperty, Application.Current!.GetResourceObservable("EditorFont"));
        Bind(FontSizeProperty, Application.Current!.GetResourceObservable("EditorFontSize"));
        // Bind(FoldingMargin.FoldingMarkerBrushProperty, Application.Current!.GetResourceObservable("ThemeBorderLowBrush"));
        // Bind(FoldingMargin.SelectedFoldingMarkerBrushProperty, Application.Current!.GetResourceObservable("ThemeControlLowBrush"));
        // Bind(FoldingMargin.SelectedFoldingMarkerBrushProperty, Application.Current!.GetResourceObservable("ThemeForegroundBrush"));
        // Bind(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, Application.Current!.GetResourceObservable("ThemeControlLowBrush"));

        Options.AllowScrollBelowDocument = true;
        Options.ConvertTabsToSpaces = true;
        Options.AllowToggleOverstrikeMode = true;

        TextArea.TextView.LinkTextUnderline = true;
        TextArea.RightClickMovesCaret = true;

        BracketRenderer = new BracketHighlightRenderer(TextArea.TextView);
        LineRenderer = new LineHighlightRenderer(this);
        //ElementGenerator = new ElementGenerator();
        //MergeService = new MergeService(this, ElementGenerator);
        WordRenderer = new WordHighlightRenderer(TextArea.TextView);
        MarkerService = new TextMarkerService(Document);
        ModificationService = new TextModificationService(TextArea.TextView);
        InlayHintGenerator = new InlayHintGenerator(this);

        TextArea.TextView.BackgroundRenderers.Add(BracketRenderer);
        TextArea.TextView.BackgroundRenderers.Add(LineRenderer);
        //TextArea.TextView.BackgroundRenderers.Add(MergeService);
        TextArea.TextView.BackgroundRenderers.Add(WordRenderer);
        TextArea.TextView.BackgroundRenderers.Add(MarkerService);

        TextArea.TextView.LineTransformers.Add(ModificationService);
        //TextArea.TextView.ElementGenerators.Add(ElementGenerator);
        TextArea.TextView.ElementGenerators.Add(InlayHintGenerator);
    }

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public TextMate.Installation? TextMateInstallation { get; private set; }
    public BracketHighlightRenderer BracketRenderer { get; }

    public LineHighlightRenderer LineRenderer { get; }

    //public MergeService MergeService { get; }
    public WordHighlightRenderer WordRenderer { get; }
    public TextMarkerService MarkerService { get; }

    public TextModificationService ModificationService { get; }

    // private ElementGenerator ElementGenerator { get; }
    public FoldingManager? FoldingManager { get; private set; }

    public InlayHintGenerator InlayHintGenerator { get; }

    protected override void OnDocumentChanged(DocumentChangedEventArgs e)
    {
        base.OnDocumentChanged(e);
        if (e?.NewDocument != null) MarkerService?.ChangeDocument(e.NewDocument);
    }

    public void InitTextmate(IRegistryOptions options)
    {
        TextMateInstallation?.Dispose();
        TextMateInstallation = this.InstallTextMate(options);
    }

    public void RemoveTextmate()
    {
        TextMateInstallation?.Dispose();
        TextMateInstallation = null;
    }

    public void SetEnableBreakpoints(bool enable, string? filePath = null)
    {
        if (TextArea.LeftMargins.Any(x => x is BreakPointLineNumberMargin))
        {
            // Der Toggle raeumt auch die eigene Spalte ab (AvaloniaEdit prueft "is
            // LineNumberMargin") und legt die Standardspalte samt Farb-Bindung neu an
            ShowLineNumbers = false;
            ShowLineNumbers = true;
        }

        if (!enable || string.IsNullOrWhiteSpace(filePath)) return;

        // Lokaler Wert schlaegt den Style-Setter -> die Zeilennummernspalte existiert danach
        // sicher, auch wenn der Editor noch nicht am Visual Tree haengt
        ShowLineNumbers = true;

        for (var i = 0; i < TextArea.LeftMargins.Count; i++)
        {
            if (TextArea.LeftMargins[i] is not LineNumberMargin) continue;
            // Entnehmen und Einfuegen statt Indexzuweisung -> das Verfahren ist mit
            // ComparisonControl belegt, ob TextArea ein Replace sauber abhaengt, nicht.
            // Der anwendungsweite Store, nicht ein eigener je Editor: sonst landen gesetzte
            // Breakpoints in einer Instanz, die weder die Breakpoint-Liste noch die laufende
            // Session je zu sehen bekommt.
            TextArea.LeftMargins.RemoveAt(i);
            TextArea.LeftMargins.Insert(i, new BreakPointLineNumberMargin(this, filePath, BreakpointStore.Instance));
            break;
        }
    }

    public void SetEnableFolding(bool enable)
    {
        if (enable)
        {
            if (FoldingManager == null) FoldingManager = FoldingManager.Install(TextArea);
        }
        else
        {
            if (FoldingManager != null) FoldingManager.Uninstall(FoldingManager);
            FoldingManager = null;
        }
    }
}
