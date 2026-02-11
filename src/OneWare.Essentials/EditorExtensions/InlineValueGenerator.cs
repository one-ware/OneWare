using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ITextSource = Avalonia.Media.TextFormatting.ITextSource;

namespace OneWare.Essentials.EditorExtensions;

public class InlineValueHint
{
    public int Offset { get; set; } = -1;
    public string Text { get; init; } = string.Empty;
}

internal class InlineValueHintWithAnchor
{
    public required InlineValueHint Hint { get; init; }
    public required TextAnchor Anchor { get; set; }
    public IBrush? Foreground { get; set; }
    public IBrush? Background { get; set; }
    public required string Text { get; set; }
}

public class InlineValueGenerator : VisualLineElementGenerator
{
    private readonly TextEditor _editor;
    private readonly List<InlineValueHintWithAnchor> _hints = [];

    public InlineValueGenerator(TextEditor editor)
    {
        _editor = editor;
    }

    public void SetInlineValues(IEnumerable<InlineValueHint> hints)
    {
        _hints.Clear();

        var foreground =
            Application.Current!.FindResource(Application.Current!.RequestedThemeVariant, "ThemeForegroundLowBrush") as
                IBrush;
        var background =
            Application.Current!.FindResource(Application.Current!.RequestedThemeVariant, "ThemeBackgroundBrush") as
                IBrush;

        _hints.AddRange(hints.Select(x => new InlineValueHintWithAnchor
        {
            Hint = x,
            Anchor = _editor.Document.CreateAnchor(x.Offset),
            Text = x.Text,
            Foreground = foreground,
            Background = background
        }));

        _editor.TextArea.TextView.Redraw();
    }

    public void ClearInlineValues()
    {
        _hints.Clear();
        _editor.TextArea.TextView.Redraw();
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var element = _hints.FirstOrDefault(x => !x.Anchor.IsDeleted && x.Anchor.Offset >= startOffset);
        return element?.Anchor.Offset ?? -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
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
