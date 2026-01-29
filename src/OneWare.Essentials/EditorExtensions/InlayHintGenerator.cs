using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ITextSource = Avalonia.Media.TextFormatting.ITextSource;

namespace OneWare.Essentials.EditorExtensions;

public class InlayHint
{
    public int Offset { get; set; } = -1;

    public string Text { get; init; } = string.Empty;
}

internal class InlayHintWithAnchor
{
    public required InlayHint Hint { get; init; }
    public required TextAnchor Anchor { get; set; }
    
    public IBrush? Foreground {get; set;}
    
    public IBrush? Background { get; set; }
    
    public required string Text { get; set; }
}

public class InlayHintGenerator : VisualLineElementGenerator
{
    private readonly TextEditor _editor;
    private readonly List<InlayHintWithAnchor> _hints = [];

    public InlayHintGenerator(TextEditor editor)
    {
        _editor = editor;
    }

    public void SetInlineHints(IEnumerable<InlayHint> hints)
    {
        _hints.Clear();

        var foreground =
            Application.Current!.FindResource(Application.Current!.RequestedThemeVariant, "ThemeForegroundLowBrush") as
                IBrush;
        var background =
            Application.Current!.FindResource(Application.Current!.RequestedThemeVariant, "ThemeBackgroundBrush") as
                IBrush;
        
        _hints.AddRange(hints.Select(x => new InlayHintWithAnchor
        {
            Hint = x,
            Anchor = _editor.Document.CreateAnchor(x.Offset),
            Text = x.Text,
            Foreground = foreground,
            Background = background
        }));

        _editor.TextArea.TextView.Redraw();
    }

    public void ClearInlineHints()
    {
        _hints.Clear();
        _editor.TextArea.TextView.Redraw();
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        // var index = _hints.BinarySearch(startOffset, (a, b) => a.CompareTo(b.Anchor.Offset));;
        //
        // if (index < 0)
        //     index = ~index;
        // if (index < _hints.Count)
        // {
        //     return  _hints[index].Anchor.Offset;
        // }
        //
        // return -1;

        var element = _hints.FirstOrDefault(x => !x.Anchor.IsDeleted && x.Anchor.Offset >= startOffset);
        
        return element?.Anchor.Offset ?? -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        // var index = _hints.BinarySearch(offset, (a, b) => a.CompareTo(b.Anchor.Offset));
        //
        // return index < 0 ? null : new InlineObjectElement(0, _hints[index].Control);
        var element = _hints.FirstOrDefault(x => !x.Anchor.IsDeleted && x.Anchor.Offset == offset);

        if (element == null) return null;
        
        var properties = new VisualLineElementTextRunProperties(CurrentContext.GlobalTextRunProperties);
        properties.SetForegroundBrush(element.Foreground);
        properties.SetBackgroundBrush(element.Background);

        return new FormattedTextElement(
            TextFormatter.Current.FormatLine(new SimpleTextSource($" {element.Text}", properties), 0, double.MaxValue,
                new GenericTextParagraphProperties(properties)), 0);
    }
    
    internal sealed class SimpleTextSource : ITextSource
    {
        private readonly string _text;
        private readonly TextRunProperties _properties;

        public SimpleTextSource(string text, TextRunProperties properties)
        {
            _text = text;
            _properties = properties;
        }

        public TextRun GetTextRun(int textSourceCharacterIndex)
        {
            if (textSourceCharacterIndex < _text.Length)
                return new TextCharacters(
                    _text.AsMemory().Slice(textSourceCharacterIndex,
                        _text.Length - textSourceCharacterIndex), _properties);
			
            return new TextEndOfParagraph(_text.Length);
        }
    }
}