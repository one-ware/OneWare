using AvaloniaEdit;
using AvaloniaEdit.Folding;
using DynamicData;

namespace OneWare.Shared.EditorExtensions;

public class ExtendedTextEditor : TextEditor
{
    protected override Type StyleKeyOverride => typeof(TextEditor);
    
    public BracketHighlightRenderer BracketRenderer { get; }
    public LineHighlightRenderer LineRenderer { get; }
    //public MergeService MergeService { get; }
    public WordHighlightRenderer WordRenderer { get; }
    public TextMarkerService MarkerService { get; }
    
    public TextModificationService ModificationService { get; }
    private ElementGenerator ElementGenerator { get; }
    public FoldingManager? FoldingManager { get; private set; }
    
    public ExtendedTextEditor()
    {
        Options.AllowScrollBelowDocument = true;
        Options.ConvertTabsToSpaces = true;
        
        TextArea.TextView.LinkTextUnderline = true;
        TextArea.RightClickMovesCaret = true;

        ElementGenerator = new ElementGenerator();
        BracketRenderer = new BracketHighlightRenderer(TextArea.TextView);
        LineRenderer = new LineHighlightRenderer(this);
        //MergeService = new MergeService(this, ElementGenerator);
        WordRenderer = new WordHighlightRenderer(TextArea.TextView);
        MarkerService = new TextMarkerService(Document);
        ModificationService = new TextModificationService(TextArea.TextView);
        
        TextArea.TextView.BackgroundRenderers.Add(BracketRenderer);
        TextArea.TextView.BackgroundRenderers.Add(LineRenderer);
        //TextArea.TextView.BackgroundRenderers.Add(MergeService);
        TextArea.TextView.BackgroundRenderers.Add(WordRenderer);
        TextArea.TextView.BackgroundRenderers.Add(MarkerService);

        TextArea.TextView.LineTransformers.Add(ModificationService);
        TextArea.TextView.ElementGenerators.Add(ElementGenerator);
    }
    
    public void SetFolding(bool active)
    {
        if (active)
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