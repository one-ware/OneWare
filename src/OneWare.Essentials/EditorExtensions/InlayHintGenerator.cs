using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using DynamicData;

namespace OneWare.Essentials.EditorExtensions;

public class InlayHint
{
    public int Offset { get; set; } = -1;
    
    public string Text { get; init; } = string.Empty;
}

internal class InlayHintWithAnchor
{
    public InlayHint Hint { get; init; }
    public TextAnchor Anchor { get; set; }
    public Control Control { get; set; }
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
        
        var foreground = Application.Current!.FindResource(Application.Current!.RequestedThemeVariant, "ThemeForegroundLowBrush") as IBrush;
        var background = Application.Current!.FindResource(Application.Current!.RequestedThemeVariant, "ThemeBackgroundBrush") as IBrush;
        
        _hints.AddRange(hints.Select(x => new InlayHintWithAnchor()
        {
            Hint = x,
            Anchor = _editor.Document.CreateAnchor(x.Offset),
            Control = new Border()
            {
                Margin = new Thickness(1, 0, 5, 0),
                Background = background,
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock()
                {
                    Text = x.Text,
                    Foreground = foreground,
                    Margin = new Thickness(2,0),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
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
        return element != null ? new InlineObjectElement(0, element.Control) : null;
    }
}