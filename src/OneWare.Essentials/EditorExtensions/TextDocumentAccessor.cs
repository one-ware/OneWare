#nullable disable

using AvaloniaEdit.Document;

namespace OneWare.Essentials.EditorExtensions
{
    /// <summary>
    ///     Interface used for the indentation class to access the document.
    /// </summary>
    public interface IDocumentAccessor
    {
        /// <summary>
        ///     Gets if the current line is read only (because it is not in the
        ///     selected text region)
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>Gets the number of the current line.</summary>
        int LineNumber { get; }

        /// <summary>Gets/Sets the text of the current line.</summary>
        string Text { get; set; }

        /// <summary>Advances to the next line.</summary>
        bool MoveNext();
    }

    #region TextDocumentAccessor

    /// <summary>
    ///     Adapter IDocumentAccessor -> TextDocument
    /// </summary>
    public sealed class TextDocumentAccessor : IDocumentAccessor
    {
        private readonly TextDocument _doc;
        private readonly int _maxLine;
        private readonly int _minLine;
        private DocumentLine _line;

        private bool _lineDirty;

        private string _text;

        /// <summary>
        ///     Creates a new TextDocumentAccessor.
        /// </summary>
        public TextDocumentAccessor(TextDocument document)
        {
            _doc = document ?? throw new ArgumentNullException(nameof(document));
            _minLine = 1;
            _maxLine = _doc.LineCount;
        }

        /// <summary>
        ///     Creates a new TextDocumentAccessor that indents only a part of the document.
        /// </summary>
        public TextDocumentAccessor(TextDocument document, int minLine, int maxLine)
        {
            _doc = document ?? throw new ArgumentNullException(nameof(document));
            _minLine = minLine;
            _maxLine = maxLine;
        }

        /// <inheritdoc />
        public bool IsReadOnly => LineNumber < _minLine;

        /// <inheritdoc />
        public int LineNumber { get; private set; }

        /// <inheritdoc />
        public string Text
        {
            get => _text;
            set
            {
                if (LineNumber < _minLine) return;
                _text = value;
                _lineDirty = true;
            }
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (_lineDirty)
            {
                _doc.Replace(_line, _text);
                _lineDirty = false;
            }

            ++LineNumber;
            if (LineNumber > _maxLine) return false;
            _line = _doc.GetLineByNumber(LineNumber);
            _text = _doc.GetText(_line);
            return true;
        }
    }

    #endregion
}