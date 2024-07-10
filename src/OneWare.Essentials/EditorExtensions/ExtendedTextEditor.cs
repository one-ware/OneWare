using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using DynamicData;
using OneWare.Essentials.Models;
using TextMateSharp.Registry;

namespace OneWare.Essentials.EditorExtensions;

public class ExtendedTextEditor : TextEditor
{
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

    protected override void OnDocumentChanged(DocumentChangedEventArgs e)
    {
        base.OnDocumentChanged(e);
        if (e?.NewDocument != null)
        {
            MarkerService?.ChangeDocument(e.NewDocument);
        }
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

    public void SetEnableBreakpoints(bool enable, IFile? file = null)
    {
        TextArea.LeftMargins.RemoveMany(TextArea.LeftMargins.Where(x => x is BreakPointMargin));
        if (enable && file != null)
        {
            TextArea.LeftMargins.Add(new BreakPointMargin(this, file, new BreakpointStore()));
        }
    }
    
    public void SetEnableFolding(bool enable)
    {
        if (enable)
        {
            if(FoldingManager == null) FoldingManager = FoldingManager.Install(TextArea);
        }
        else
        {
            if (FoldingManager != null) FoldingManager.Uninstall(FoldingManager);
            FoldingManager = null;
        }
    }
}