using System.Text;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared.EditorExtensions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Shared.LanguageService
{
    /// <summary>
    ///     Implement shared functions between type assistances here
    /// </summary>
    public abstract class TypeAssistance
    {
        public virtual bool CanAddBreakPoints => false;
        
        public IEditor Editor { get; }
        public TextEditor CodeBox => Editor.Editor;
        public IFile CurrentFile => Editor.CurrentFile ?? throw new NullReferenceException(nameof(Editor.CurrentFile));
        
        public IIndentationStrategy? IndentationStrategy { get; protected set; }

        public CompletionWindow? Completion { get; set; }

        public OverloadInsightWindow? OverloadInsight { get; set; }

        public TextInputWindow? TextInput { get; set; }

        public IFoldingStrategy? FoldingStrategy { get; set; }
        
        protected bool IsOpen { get; private set; }
        protected bool IsAttached { get; private set; }

        public event EventHandler? AssistanceActivated;
        public event EventHandler? AssistanceDeactivated;

        public TypeAssistance(IEditor editor)
        {
            Editor = editor;
        }
        
        protected virtual void OnServerActivated()
        {
            AssistanceActivated?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnServerDeactivated()
        {
            AssistanceDeactivated?.Invoke(this, EventArgs.Empty);
        }

        public virtual void Open()
        {
            IsOpen = true;
        }

        public virtual void Close()
        {
            IsOpen = false;
        }
        
        public virtual void Attach(CompletionWindow completionWindow)
        {
            Completion = completionWindow;
            IsAttached = true;
        }

        public virtual void Detach()
        {
            Completion = null;
            IsAttached = false;
        }

        public virtual Task TextEnteredAsync(TextInputEventArgs e)
        {
            return Task.CompletedTask;
        }

        public virtual void TextEntering(TextInputEventArgs e)
        {
        }

        public virtual void Format()
        {
            if (CodeBox.Document != null) Format(1, CodeBox.Document.LineCount);
        }

        public virtual void Format(int startLine, int endLine)
        {
            CodeBox.Document?.BeginUpdate();
            IndentationStrategy?.IndentLines(CodeBox.Document, startLine, endLine);
            CodeBox.Document?.EndUpdate();
        }

        protected virtual IEnumerable<TextDocumentContentChangeEvent> ConvertChanges(DocumentChangeEventArgs e)
        {
            var l = new List<TextDocumentContentChangeEvent>();
            var map = e.OffsetChangeMap;

            //Console.WriteLine("??" + map.Count + " " + e.Offset + " " + e.InsertedText);

            if (map.Count <= 1)
            {
                var m = e;
                var location = CodeBox.Document.GetLocation(m.Offset);
                //calculate newlines
                var newlines = e.RemovedText.Text.Count(x => x == '\n');
                var lastIndexNewLine = e.RemovedText.Text.LastIndexOf('\n');
                var lengthAfterLastNewLine = lastIndexNewLine >= 0
                    ? e.RemovedText.TextLength - lastIndexNewLine
                    : location.Column + e.RemovedText.TextLength;

                //Console.WriteLine("moff: " + m.Offset + " RemovalLength: " + m.RemovalLength + " DeletedLines: " + newlines + " lastLineLength: " + lengthAfterLastNewLine + " s: " + Regex.Escape(e.RemovedText.Text));
                var endoffset = m.Offset;
                endoffset += m.RemovalLength;

                var endlocation = new TextLocation(location.Line + newlines, lengthAfterLastNewLine);

                var docChange = new TextDocumentContentChangeEvent
                {
                    Range = new Range
                    {
                        Start = new Position(location.Line - 1, location.Column - 1),
                        End = new Position(endlocation.Line - 1, endlocation.Column - 1)
                    },
                    Text = e.InsertedText.Text,
                    RangeLength = m.RemovalLength
                };

                l.Add(docChange);
                //Console.WriteLine("c Start: " + docChange.Range.Start.Line + " " + docChange.Range.Start.Character + " End: " + docChange.Range.End.Line + " " + docChange.Range.End.Character + " T: " + e.InsertedText.Text + " " + e.InsertedText.Text.Length);
            }
            else
            {
                throw new NotSupportedException("Multiple offsets???");
            }

            return l;
        }

        protected bool AllowedChars(char c)
        {
            return c is '[' or '(';
        }

        #region Comment

        /// <summary>
        ///     Comments selection
        /// </summary>
        public virtual void Comment()
        {
            if (!(this is ITypeAssistance ita)) return;

            int startLine, endLine;
            if (!CodeBox.TextArea.Selection.IsEmpty)
            {
                startLine = CodeBox.Document.GetLineByOffset(CodeBox.SelectionStart).LineNumber;
                endLine = CodeBox.Document.GetLineByOffset(CodeBox.SelectionStart + CodeBox.SelectionLength).LineNumber;
            }
            else
            {
                if (CodeBox.TextArea.Caret.Offset <= 0 ||
                    CodeBox.TextArea.Caret.Offset > CodeBox.Document.TextLength) return;
                startLine = CodeBox.TextArea.Caret.Line;
                endLine = startLine;
            }

            CodeBox.Document.BeginUpdate();
            for (var i = startLine; i <= endLine; i++)
                CodeBox.Document.Replace(CodeBox.Document.Lines[i - 1].Offset, 0, ita.LineCommentSequence);
            CodeBox.Document.EndUpdate();
        }

        /// <summary>
        ///     Uncomments selection
        /// </summary>
        public virtual void Uncomment()
        {
            if (!(this is ITypeAssistance ita)) return;

            int startLine, endLine;
            if (!CodeBox.TextArea.Selection.IsEmpty)
            {
                startLine = CodeBox.Document.GetLineByOffset(CodeBox.SelectionStart).LineNumber;
                endLine = CodeBox.Document.GetLineByOffset(CodeBox.SelectionStart + CodeBox.SelectionLength).LineNumber;
            }
            else
            {
                if (CodeBox.TextArea.Caret.Offset <= 0 ||
                    CodeBox.TextArea.Caret.Offset > CodeBox.Document.TextLength) return;
                startLine = CodeBox.TextArea.Caret.Line;
                endLine = startLine;
            }

            CodeBox.Document.BeginUpdate();
            for (var i = startLine; i <= endLine; i++)
            {
                var line = CodeBox.Document.GetText(CodeBox.Document.Lines[i - 1].Offset,
                    CodeBox.Document.Lines[i - 1].Length);
                if (!line.Trim().StartsWith(ita.LineCommentSequence)) continue;
                var index = line.IndexOf(ita.LineCommentSequence);
                CodeBox.Document.Replace(CodeBox.Document.Lines[i - 1].Offset + index, ita.LineCommentSequence.Length,
                    "");
            }

            CodeBox.Document.EndUpdate();
        }

        #endregion

        protected string LastWord(int index)
        {
            if (index >= CodeBox.Text.Length) return string.Empty;
            var sb = new StringBuilder();
            var firstChar = false;
            for (var i = index; i >= 0; i--)
            {
                var c = CodeBox.Text[i];
                if (c is ' ')
                {
                    if (!firstChar) continue;
                    break;
                }
                firstChar = true;
                sb.Insert(0, CodeBox.Text[i]);
            }
            return sb.ToString();
        }
    }
}