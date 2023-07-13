using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Indentation;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.Models;

namespace OneWare.Shared.LanguageService
{
    public abstract class TypeAssistance : ITypeAssistance
    {
        public virtual bool CanAddBreakPoints => false;
        public string? LineCommentSequence { get; protected set; }
        protected IEditor Editor { get; }
        protected TextEditor CodeBox => Editor.Editor;
        protected IFile CurrentFile => Editor.CurrentFile ?? throw new NullReferenceException(nameof(Editor.CurrentFile));
        
        protected IIndentationStrategy? IndentationStrategy { get; init; }
        
        protected IFormattingStrategy? FormattingStrategy { get; init; }

        protected CompletionWindow? Completion { get; private set; }

        protected OverloadInsightWindow? OverloadInsight { get; set; }

        protected TextInputWindow? TextInput { get; set; }

        public IFoldingStrategy? FoldingStrategy { get; protected init; }
        
        protected bool IsOpen { get; private set; }
        protected bool IsAttached { get; private set; }

        public event EventHandler? AssistanceActivated;
        public event EventHandler? AssistanceDeactivated;

        public TypeAssistance(IEditor editor)
        {
            Editor = editor;
        }
        
        protected virtual void OnAssistanceActivated()
        {
            AssistanceActivated?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnAssistanceDeactivated()
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

        public virtual Task<List<MenuItemModel>?> GetQuickMenuAsync(int offset)
        {
            throw new NotImplementedException();
        }

        public virtual Task<string?> GetHoverInfoAsync(int offset)
        {
            return Task.FromResult<string?>(null);
        }

        public virtual Task<Action?> GetActionOnControlWordAsync(int offset)
        {
            return Task.FromResult<Action?>(null);
        }

        public virtual IEnumerable<MenuItemModel>? GetTypeAssistanceQuickOptions()
        {
            return null;
        }

        public virtual void AutoIndent()
        {
            if (CodeBox.Document != null) AutoIndent(1, CodeBox.Document.LineCount);
        }

        public virtual void AutoIndent(int startLine, int endLine)
        {
            CodeBox.Document?.BeginUpdate();
            IndentationStrategy?.IndentLines(CodeBox.Document, startLine, endLine);
            CodeBox.Document?.EndUpdate();
        }

        public virtual void Format()
        {
            if(FormattingStrategy != null) FormattingStrategy.Format(CodeBox.Document);
            else IndentationStrategy?.IndentLines(CodeBox.Document, 1, CodeBox.Document.LineCount);
        }

        #region Comment
        
        public virtual void Comment()
        {
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
                CodeBox.Document.Replace(CodeBox.Document.Lines[i - 1].Offset, 0, LineCommentSequence);
            CodeBox.Document.EndUpdate();
        }
        
        public virtual void Uncomment()
        {
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
                if (!line.Trim().StartsWith(LineCommentSequence)) continue;
                var index = line.IndexOf(LineCommentSequence, StringComparison.Ordinal);
                CodeBox.Document.Replace(CodeBox.Document.Lines[i - 1].Offset + index, LineCommentSequence.Length,
                    "");
            }

            CodeBox.Document.EndUpdate();
        }

        #endregion
    }
}