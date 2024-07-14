using Avalonia.Input;
using AvaloniaEdit;

namespace OneWare.Core.Extensions;

public static class TextEditorExtensions
{
    public static string? GetWordAtMousePos(this TextEditor editor, PointerEventArgs e)
    {
        var pos = editor.GetPositionFromPoint(e.GetPosition(editor)); //gets position of mouse
        if (pos != null)
        {
            var element = "";
            var start = editor.Document.GetOffset(pos.Value.Location); //gets offset in text of mouse position
            for (;
                 start > -1 && start < editor.Text.Length &&
                 (char.IsLetterOrDigit(editor.Text[start]) || editor.Text[start] == '_');
                 start--)
            {
            } //finds start of word

            start++;
            var end = editor.Document.GetOffset(pos.Value.Location);
            for (;
                 end < editor.Text.Length && (char.IsLetterOrDigit(editor.Text[end]) || editor.Text[end] == '_');
                 end++)
            {
            } //finds end of word

            if (start > -1 && end < editor.Text.Length && start < end) element = editor.Text[start..end];
            var word = element;

            return word;
        }

        return null;
    }
}