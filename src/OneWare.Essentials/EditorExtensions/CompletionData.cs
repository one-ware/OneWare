using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Essentials.EditorExtensions
{
    public class CompletionData : ICompletionData
    {
        public readonly CompletionItem? CompletionItemLsp;

        private Action? AfterCompletion { get; }

        public int CompletionOffset { get; }

        public IImage? Image { get; }

        public string Text { get; private set; }

        public object Content { get; }

        public object? Description { get; }

        public double Priority { get; }
        
        public CompletionData(string insertText, string label, string? description, IImage? icon, double priority,
            CompletionItem completionItem, int offset, Action? afterCompletion = null)
        {
            Text = insertText;
            Content = label;
            Description = description;
            Image = icon;
            Priority = priority;
            CompletionItemLsp = completionItem;
            AfterCompletion = afterCompletion;
            CompletionOffset = offset;
        }
        
        public CompletionData(string insertText, string label, string? description, IImage? icon, double priority, int offset, Action? afterCompletion = null)
        {
            Text = insertText;
            Content = label;
            Description = description;
            Image = icon;
            Priority = priority;
            AfterCompletion = afterCompletion;
            CompletionOffset = offset;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var segmentLine = textArea.Document.GetLineByOffset(completionSegment.Offset);

            var caretStop = new Regex(@"\$.");
            var placeHolder = new Regex(@"\${.*?}");

            var newLine = TextUtilities.GetNewLineFromDocument(textArea.Document, segmentLine.LineNumber);
            
            var formatted = Text.Replace("\n", newLine);

            if (!caretStop.Match(formatted).Success && !placeHolder.Match(formatted).Success) formatted += "${1}";

            var startLine = segmentLine.LineNumber;
            var endLine = segmentLine.LineNumber + formatted.Split('\n').Length - 1;

            textArea.Document.BeginUpdate();
            if (textArea.Caret.Offset > CompletionOffset - 1)
                textArea.Document.Replace(CompletionOffset - 1, textArea.Caret.Offset - (CompletionOffset - 1),
                    ""); //Remove async
            textArea.Document.Replace(completionSegment, formatted);

            var start = textArea.Document.GetLineByNumber(startLine);
            var end = textArea.Document.GetLineByNumber(endLine);

            textArea.IndentationStrategy?.IndentLines(textArea.Document, segmentLine.LineNumber, endLine);

            var newText = textArea.Document.Text.Substring(start.Offset, end.EndOffset - start.Offset);

            var newCaretOffset = -1;
            //Set caret
            var firstCaretStop = caretStop.Match(newText);
            var firstPlaceHolderStop = placeHolder.Match(newText);

            if (firstCaretStop.Success)
                newCaretOffset = start.Offset + firstCaretStop.Index;
            else if (firstPlaceHolderStop.Success)
                newCaretOffset = start.Offset + firstPlaceHolderStop.Index;
            else
                newCaretOffset = start.Offset + newText.IndexOf(Text, StringComparison.Ordinal) + formatted.Length;

            //Remove placeholders & caretstops
            var filteredText = placeHolder.Replace(newText, "");
            filteredText = caretStop.Replace(filteredText, "");

            textArea.Document.Replace(start.Offset, end.EndOffset - start.Offset, filteredText);

            textArea.Document.EndUpdate();

            if (newCaretOffset >= 0) textArea.Caret.Offset = newCaretOffset;
            
            textArea.Caret.BringCaretToView(50);

            AfterCompletion?.Invoke();
        }
    }
}