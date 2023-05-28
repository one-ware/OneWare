using System.Linq;
using Avalonia.Input;
using AvaloniaEdit;

namespace OneWare.Core.Extensions;

public static class TextEditorExtensions
{
    /// <summary>
    ///     used for CodeBox_MouseRightButtonDown to find the word that is clicked on
    /// </summary>
    /// <returns></returns>
    public static string? GetWordAtMousePos(this TextEditor editor, PointerEventArgs e)
    {
        var pos = editor.GetPositionFromPoint(e.GetPosition(editor)); //gets position of mouse
        if (pos != null)
        {
            var element = "";
            var start = editor.Document.GetOffset(pos.Value.Location); //gets offset in text of mouse position
            for (;
                 start > -1 && start < editor.Text.Length && (!char.IsWhiteSpace(editor.Text.ElementAt(start)));
                 start--)
            {
            } //finds start of word

            start++;
            var end = editor.Document.GetOffset(pos.Value.Location);
            for (;
                 end < editor.Text.Length && (!char.IsWhiteSpace(editor.Text.ElementAt(end)));
                 end++)
            {
            } //finds end of word

            if (start > -1 && end < editor.Text.Length && start < end) element = editor.Text[start..end];
            var word = element;
            start--;
            if (start > -1)
            {
                for (; start > 0 && start < editor.Text.Length && editor.Text.ElementAt(start) < 33; start--)
                {
                }

                for (;
                     start > 0 && start < editor.Text.Length &&
                     (!char.IsWhiteSpace(editor.Text.ElementAt(start)));
                     start--)
                {
                }
                //if (editor.Text.IndexOf("New", start, StringComparison.OrdinalIgnoreCase) == start + 1) ; //newCompFunc = true;        //checks if the word before is NewComponent or NewFunction
            }

            return word;
        }

        return null;
    }
}