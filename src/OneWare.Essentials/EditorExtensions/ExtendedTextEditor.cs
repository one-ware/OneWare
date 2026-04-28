using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using DynamicData;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Services;
using TextMateSharp.Registry;
using RoutedCommand = AvaloniaEdit.RoutedCommand;

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

        DisableDefaultKeyGestures(TextArea);
    }

    private static readonly object RegistrationLock = new();
    private static bool _commandsRegistered;

    /// <summary>
    ///     Removes all <see cref="KeyBinding" />s registered by AvaloniaEdit's default input
    ///     handlers. The corresponding <see cref="RoutedCommand" />s and their command bindings
    ///     are kept so they can still be invoked programmatically (for example through
    ///     <see cref="RoutedEditorCommand" />), but the hard-coded gestures no longer fire.
    /// </summary>
    public static void DisableDefaultKeyGestures(TextArea textArea)
    {
        ArgumentNullException.ThrowIfNull(textArea);

        foreach (var handler in EnumerateDefaultInputHandlers(textArea.DefaultInputHandler))
            handler.KeyBindings.Clear();
    }

    /// <summary>
    ///     Registers an <see cref="RoutedEditorCommand" /> for every <see cref="RoutedCommand" />
    ///     that AvaloniaEdit's default input handlers ship with so they can be re-bound through
    ///     the application command system. Safe to call multiple times.
    /// </summary>
    public static void RegisterDefaultEditorCommands(IApplicationCommandService commandService)
    {
        ArgumentNullException.ThrowIfNull(commandService);

        lock (RegistrationLock)
        {
            if (_commandsRegistered) return;
            _commandsRegistered = true;
        }

        // A throwaway TextArea is used to enumerate the bindings AvaloniaEdit ships with.
        var textArea = new TextArea();
        var seen = new HashSet<RoutedCommand>();

        foreach (var handler in EnumerateDefaultInputHandlers(textArea.DefaultInputHandler))
        {
            foreach (var keyBinding in handler.KeyBindings)
            {
                if (keyBinding.Command is not RoutedCommand routedCommand) continue;
                if (!seen.Add(routedCommand)) continue;

                commandService.RegisterCommand(new RoutedEditorCommand(routedCommand)
                {
                    DefaultGesture = keyBinding.Gesture
                });
            }
        }
    }

    private static IEnumerable<TextAreaInputHandler> EnumerateDefaultInputHandlers(
        TextAreaDefaultInputHandler defaultInputHandler)
    {
        yield return defaultInputHandler;
        yield return defaultInputHandler.CaretNavigation;
        yield return defaultInputHandler.Editing;
        foreach (var nested in defaultInputHandler.NestedInputHandlers)
            if (nested is TextAreaInputHandler tah)
                yield return tah;
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
        TextArea.LeftMargins.RemoveMany(TextArea.LeftMargins.Where(x => x is BreakPointMargin));
        if (enable && !string.IsNullOrWhiteSpace(filePath))
            TextArea.LeftMargins.Add(new BreakPointMargin(this, filePath, new BreakpointStore()));
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
