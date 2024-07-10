using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace OneWare.Essentials.EditorExtensions
{
    public class BracketHighlightRenderer : IBackgroundRenderer
    {
        public const string BracketHighlight = "Bracket highlight";

        public static readonly Color DefaultBackground = Color.FromArgb(50, 0, 200, 255);
        public static readonly Color DefaultBorder = Color.FromArgb(150, 0, 200, 255);
        private readonly TextView _textView;
        private Brush? _backgroundBrush;
        private Pen? _borderPen;
        private BracketSearchResult? _result;

        public BracketHighlightRenderer(TextView textView)
        {
            this._textView = textView ?? throw new ArgumentNullException("textView");

            this._textView.BackgroundRenderers.Add(this);
        }

        public KnownLayer Layer => KnownLayer.Caret;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_result == null)
                return;

            var builder = new BackgroundGeometryBuilder
            {
                CornerRadius = 1,
                AlignToWholePixels = true
            };

            builder.AddSegment(textView,
                new TextSegment { StartOffset = _result.OpeningBracketOffset, Length = _result.OpeningBracketLength });
            builder.CloseFigure(); // prevent connecting the two segments
            builder.AddSegment(textView,
                new TextSegment { StartOffset = _result.ClosingBracketOffset, Length = _result.ClosingBracketLength });

            var geometry = builder.CreateGeometry();

            if (_borderPen == null)
                UpdateColors(DefaultBackground, DefaultBackground);

            if (geometry != null) drawingContext.DrawGeometry(_backgroundBrush, _borderPen, geometry);
        }

        public void SetHighlight(BracketSearchResult? result)
        {
            if (this._result != result)
            {
                this._result = result;
                _textView.InvalidateLayer(Layer);
            }
        }

        private void UpdateColors(Color background, Color foreground)
        {
            _borderPen = new Pen(new SolidColorBrush(foreground));
            //this.borderPen.Freeze();

            _backgroundBrush = new SolidColorBrush(background);
            //this.backgroundBrush.Freeze();
        }

        public static void ApplyCustomizationsToRendering(BracketHighlightRenderer renderer,
            IEnumerable<Color> customizations)
        {
            renderer.UpdateColors(DefaultBackground, DefaultBorder);
            foreach (var color in customizations)
            {
                //if (color.Name == BracketHighlight) {
                renderer.UpdateColors(color, color);
                //                    renderer.UpdateColors(color.Background ?? Colors.Blue, color.Foreground ?? Colors.Blue);
                // 'break;' is necessary because more specific customizations come first in the list
                // (language-specific customizations are first, followed by 'all languages' customizations)
                break;
                //}
            }
        }
    }

    /// <summary>
    ///     Allows language specific search for matching brackets.
    /// </summary>
    public interface IBracketSearcher
    {
        /// <summary>
        ///     Searches for a matching bracket from the given offset to the start of the document.
        /// </summary>
        /// <returns>
        ///     A BracketSearchResult that contains the positions and lengths of the brackets. Return null if there is nothing
        ///     to highlight.
        /// </returns>
        BracketSearchResult? SearchBracket(TextDocument document, int offset);
    }

    public class DefaultBracketSearcher : IBracketSearcher
    {
        public static readonly DefaultBracketSearcher DefaultInstance = new();

        public BracketSearchResult? SearchBracket(TextDocument document, int offset)
        {
            return null;
        }
    }

    /// <summary>
    ///     Describes a pair of matching brackets found by IBracketSearcher.
    /// </summary>
    public class BracketSearchResult
    {
        public BracketSearchResult(int openingBracketOffset, int openingBracketLength,
            int closingBracketOffset, int closingBracketLength)
        {
            OpeningBracketOffset = openingBracketOffset;
            OpeningBracketLength = openingBracketLength;
            ClosingBracketOffset = closingBracketOffset;
            ClosingBracketLength = closingBracketLength;
        }

        public int OpeningBracketOffset { get; }

        public int OpeningBracketLength { get; }

        public int ClosingBracketOffset { get; }

        public int ClosingBracketLength { get; }
    }


    /// <summary>
    ///     Searches matching brackets .
    /// </summary>
    public class CBracketSearcher : IBracketSearcher
    {
        private readonly string _closingBrackets = ")]}";
        private readonly string _openingBrackets = "([{";

        public BracketSearchResult? SearchBracket(TextDocument document, int offset)
        {
            if (offset > 0)
            {
                var c = document.GetCharAt(offset - 1);
                var index = _openingBrackets.IndexOf(c);
                var otherOffset = -1;
                if (index > -1)
                    otherOffset =
                        SearchBracketForward(document, offset, _openingBrackets[index], _closingBrackets[index]);

                index = _closingBrackets.IndexOf(c);
                if (index > -1)
                    otherOffset = SearchBracketBackward(document, offset - 2, _openingBrackets[index],
                        _closingBrackets[index]);

                if (otherOffset > -1)
                {
                    var result = new BracketSearchResult(Math.Min(offset - 1, otherOffset), 1,
                        Math.Max(offset - 1, otherOffset), 1);
                    //SearchDefinition(document, result);
                    return result;
                }
            }

            return null;
        }

        private void SearchDefinition(IDocument document, BracketSearchResult result)
        {
            if (document.GetCharAt(result.OpeningBracketOffset) != '{')
                return;
            // get line
            var documentLine = document.GetLineByOffset(result.OpeningBracketOffset);
            while (documentLine != null && IsBracketOnly(document, documentLine))
                documentLine = documentLine.PreviousLine;
            if (documentLine != null)
            {
                //result. = documentLine.Offset;
                //result.DefinitionHeaderLength = documentLine.Length;
            }
        }

        private bool IsBracketOnly(IDocument document, IDocumentLine documentLine)
        {
            var lineText = document.GetText(documentLine).Trim();
            return lineText == "{" || string.IsNullOrEmpty(lineText)
                                   || lineText.StartsWith("//", StringComparison.Ordinal)
                                   || lineText.StartsWith("/*", StringComparison.Ordinal)
                                   || lineText.StartsWith("*", StringComparison.Ordinal)
                                   || lineText.StartsWith("'", StringComparison.Ordinal);
        }

        #region SearchBracketBackward

        private int SearchBracketBackward(IDocument document, int offset, char openBracket, char closingBracket)
        {
            if (offset + 1 >= document.TextLength) return -1;
            // this method parses a c# document backwards to find the matching bracket

            // first try "quick find" - find the matching bracket if there is no string/comment in the way
            var quickResult = QuickSearchBracketBackward(document, offset, openBracket, closingBracket);
            if (quickResult >= 0) return quickResult;

            // we need to parse the line from the beginning, so get the line start position
            var linestart = ScanLineStart(document, offset + 1);

            // we need to know where offset is - in a string/comment or in normal code?
            // ignore cases where offset is in a block comment
            var starttype = GetStartType(document, linestart, offset + 1);
            if (starttype == 1) return -1; // start position is in a comment

            // I don't see any possibility to parse a C# document backwards...
            // We have to do it forwards and push all bracket positions on a stack.
            var bracketStack = new Stack<int>();
            var blockComment = false;
            var lineComment = false;
            var inChar = false;
            var inString = false;
            var verbatim = false;

            for (var i = 0; i <= offset; ++i)
            {
                var ch = document.GetCharAt(i);
                switch (ch)
                {
                    case '\r':
                    case '\n':
                        lineComment = false;
                        inChar = false;
                        if (!verbatim) inString = false;
                        break;
                    case '/':
                        if (blockComment)
                            if (document.GetCharAt(i - 1) == '*')
                                blockComment = false;
                        if (!inString && !inChar && i + 1 < document.TextLength)
                        {
                            if (!blockComment && document.GetCharAt(i + 1) == '/') lineComment = true;
                            if (!lineComment && document.GetCharAt(i + 1) == '*') blockComment = true;
                        }

                        break;
                    case '"':
                        if (!(inChar || lineComment || blockComment))
                        {
                            if (inString && verbatim)
                            {
                                if (i + 1 < document.TextLength && document.GetCharAt(i + 1) == '"')
                                {
                                    ++i; // skip escaped quote
                                    inString = false; // let the string go
                                }
                                else
                                {
                                    verbatim = false;
                                }
                            }
                            else if (!inString && offset > 0 && document.GetCharAt(i - 1) == '@')
                            {
                                verbatim = true;
                            }

                            inString = !inString;
                        }

                        break;
                    case '\'':
                        if (!(inString || lineComment || blockComment)) inChar = !inChar;
                        break;
                    case '\\':
                        if (inString && !verbatim || inChar)
                            ++i; // skip next character
                        break;
                    default:
                        if (ch == openBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment)) bracketStack.Push(i);
                        }
                        else if (ch == closingBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment))
                                if (bracketStack.Count > 0)
                                    bracketStack.Pop();
                        }

                        break;
                }
            }

            if (bracketStack.Count > 0) return bracketStack.Pop();
            return -1;
        }

        #endregion

        #region SearchBracketForward

        private int SearchBracketForward(IDocument document, int offset, char openBracket, char closingBracket)
        {
            var inString = false;
            var inChar = false;
            var verbatim = false;

            var lineComment = false;
            var blockComment = false;

            if (offset < 0) return -1;

            // first try "quick find" - find the matching bracket if there is no string/comment in the way
            var quickResult = QuickSearchBracketForward(document, offset, openBracket, closingBracket);
            if (quickResult >= 0) return quickResult;

            // we need to parse the line from the beginning, so get the line start position
            var linestart = ScanLineStart(document, offset);

            // we need to know where offset is - in a string/comment or in normal code?
            // ignore cases where offset is in a block comment
            var starttype = GetStartType(document, linestart, offset);
            if (starttype != 0) return -1; // start position is in a comment/string

            var brackets = 1;

            while (offset < document.TextLength)
            {
                var ch = document.GetCharAt(offset);
                switch (ch)
                {
                    case '\r':
                    case '\n':
                        lineComment = false;
                        inChar = false;
                        if (!verbatim) inString = false;
                        break;
                    case '/':
                        if (blockComment)
                            if (document.GetCharAt(offset - 1) == '*')
                                blockComment = false;
                        if (!inString && !inChar && offset + 1 < document.TextLength)
                        {
                            if (!blockComment && document.GetCharAt(offset + 1) == '/') lineComment = true;
                            if (!lineComment && document.GetCharAt(offset + 1) == '*') blockComment = true;
                        }

                        break;
                    case '"':
                        if (!(inChar || lineComment || blockComment))
                        {
                            if (inString && verbatim)
                            {
                                if (offset + 1 < document.TextLength && document.GetCharAt(offset + 1) == '"')
                                {
                                    ++offset; // skip escaped quote
                                    inString = false; // let the string go
                                }
                                else
                                {
                                    verbatim = false;
                                }
                            }
                            else if (!inString && offset > 0 && document.GetCharAt(offset - 1) == '@')
                            {
                                verbatim = true;
                            }

                            inString = !inString;
                        }

                        break;
                    case '\'':
                        if (!(inString || lineComment || blockComment)) inChar = !inChar;
                        break;
                    case '\\':
                        if (inString && !verbatim || inChar)
                            ++offset; // skip next character
                        break;
                    default:
                        if (ch == openBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment)) ++brackets;
                        }
                        else if (ch == closingBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment))
                            {
                                --brackets;
                                if (brackets == 0) return offset;
                            }
                        }

                        break;
                }

                ++offset;
            }

            return -1;
        }

        #endregion

        private int QuickSearchBracketBackward(IDocument document, int offset, char openBracket, char closingBracket)
        {
            var brackets = -1;
            // first try "quick find" - find the matching bracket if there is no string/comment in the way
            for (var i = offset; i >= 0; --i)
            {
                var ch = document.GetCharAt(i);
                if (ch == openBracket)
                {
                    ++brackets;
                    if (brackets == 0) return i;
                }
                else if (ch == closingBracket)
                {
                    --brackets;
                }
                else if (ch == '"')
                {
                    break;
                }
                else if (ch == '\'')
                {
                    break;
                }
                else if (ch == '/' && i > 0)
                {
                    if (document.GetCharAt(i - 1) == '/') break;
                    if (document.GetCharAt(i - 1) == '*') break;
                }
            }

            return -1;
        }

        private int QuickSearchBracketForward(IDocument document, int offset, char openBracket, char closingBracket)
        {
            var brackets = 1;
            // try "quick find" - find the matching bracket if there is no string/comment in the way
            for (var i = offset; i < document.TextLength; ++i)
            {
                var ch = document.GetCharAt(i);
                if (ch == openBracket)
                {
                    ++brackets;
                }
                else if (ch == closingBracket)
                {
                    --brackets;
                    if (brackets == 0) return i;
                }
                else if (ch == '"')
                {
                    break;
                }
                else if (ch == '\'')
                {
                    break;
                }
                else if (ch == '/' && i > 0)
                {
                    if (document.GetCharAt(i - 1) == '/') break;
                }
                else if (ch == '*' && i > 0)
                {
                    if (document.GetCharAt(i - 1) == '/') break;
                }
            }

            return -1;
        }

        #region SearchBracket helper functions

        private static int ScanLineStart(IDocument document, int offset)
        {
            for (var i = offset - 1; i > 0; --i)
                if (document.GetCharAt(i) == '\n')
                    return i + 1;
            return 0;
        }

        /// <summary>
        ///     Gets the type of code at offset.<br />
        ///     0 = Code,<br />
        ///     1 = Comment,<br />
        ///     2 = String<br />
        ///     Block comments and multiline strings are not supported.
        /// </summary>
        private static int GetStartType(IDocument document, int linestart, int offset)
        {
            var inString = false;
            var inChar = false;
            var verbatim = false;
            var result = 0;
            for (var i = linestart; i < offset; i++)
                switch (document.GetCharAt(i))
                {
                    case '/':
                        if (!inString && !inChar && i + 1 < document.TextLength)
                            if (document.GetCharAt(i + 1) == '/')
                                result = 1;
                        break;
                    case '"':
                        if (!inChar)
                        {
                            if (inString && verbatim)
                            {
                                if (i + 1 < document.TextLength && document.GetCharAt(i + 1) == '"')
                                {
                                    ++i; // skip escaped quote
                                    inString = false; // let the string go on
                                }
                                else
                                {
                                    verbatim = false;
                                }
                            }
                            else if (!inString && i > 0 && document.GetCharAt(i - 1) == '@')
                            {
                                verbatim = true;
                            }

                            inString = !inString;
                        }

                        break;
                    case '\'':
                        if (!inString) inChar = !inChar;
                        break;
                    case '\\':
                        if (inString && !verbatim || inChar)
                            ++i; // skip next character
                        break;
                }

            return inString || inChar ? 2 : result;
        }

        #endregion
    }

    public class VhdpBracketSearcher : IBracketSearcher
    {
        private readonly string _closingBrackets = ")]}";
        private readonly string _openingBrackets = "([{";

        public BracketSearchResult? SearchBracket(TextDocument document, int offset)
        {
            if (offset > 0)
            {
                var c = document.GetCharAt(offset - 1);
                var index = _openingBrackets.IndexOf(c);
                var otherOffset = -1;
                if (index > -1)
                    otherOffset =
                        SearchBracketForward(document, offset, _openingBrackets[index], _closingBrackets[index]);

                index = _closingBrackets.IndexOf(c);
                if (index > -1)
                    otherOffset = SearchBracketBackward(document, offset - 2, _openingBrackets[index],
                        _closingBrackets[index]);

                if (otherOffset > -1)
                    return new BracketSearchResult(Math.Min(offset - 1, otherOffset), 1,
                        Math.Max(offset - 1, otherOffset), 1);
            }

            return null;
        }

        #region SearchBracketBackward

        private int SearchBracketBackward(TextDocument document, int offset, char openBracket, char closingBracket)
        {
            if (offset + 1 >= document.TextLength) return -1;
            // this method parses a c# document backwards to find the matching bracket

            // first try "quick find" - find the matching bracket if there is no string/comment in the way
            var quickResult = QuickSearchBracketBackward(document, offset, openBracket, closingBracket);
            if (quickResult >= 0) return quickResult;

            // we need to parse the line from the beginning, so get the line start position
            var linestart = ScanLineStart(document, offset + 1);

            // we need to know where offset is - in a string/comment or in normal code?
            // ignore cases where offset is in a block comment
            var starttype = GetStartType(document, linestart, offset + 1);
            if (starttype == 1) return -1; // start position is in a comment

            // I don't see any possibility to parse a C# document backwards...
            // We have to do it forwards and push all bracket positions on a stack.
            var bracketStack = new Stack<int>();
            var blockComment = false;
            var lineComment = false;
            var inChar = false;
            var inString = false;
            var verbatim = false;

            for (var i = 0; i <= offset; ++i)
            {
                var ch = document.GetCharAt(i);
                switch (ch)
                {
                    case '\r':
                    case '\n':
                        lineComment = false;
                        inChar = false;
                        if (!verbatim) inString = false;
                        break;
                    case '/':
                        if (blockComment)
                            if (document.GetCharAt(i - 1) == '*')
                                blockComment = false;
                        if (!inString && !inChar && i + 1 < document.TextLength)
                            if (!lineComment && document.GetCharAt(i + 1) == '*')
                                blockComment = true;
                        break;
                    case '-':
                        if (!inString && !inChar && i + 1 < document.TextLength)
                            if (!blockComment && document.GetCharAt(i + 1) == '-')
                                lineComment = true;
                        break;
                    default:
                        if (ch == openBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment)) bracketStack.Push(i);
                        }
                        else if (ch == closingBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment))
                                if (bracketStack.Count > 0)
                                    bracketStack.Pop();
                        }

                        break;
                }
            }

            if (bracketStack.Count > 0) return bracketStack.Pop();
            return -1;
        }

        #endregion

        #region SearchBracketForward

        private int SearchBracketForward(TextDocument document, int offset, char openBracket, char closingBracket)
        {
            var inString = false;
            var inChar = false;
            var verbatim = false;

            var lineComment = false;
            var blockComment = false;

            if (offset < 0) return -1;

            // first try "quick find" - find the matching bracket if there is no string/comment in the way
            var quickResult = QuickSearchBracketForward(document, offset, openBracket, closingBracket);
            if (quickResult >= 0) return quickResult;

            // we need to parse the line from the beginning, so get the line start position
            var linestart = ScanLineStart(document, offset);

            // we need to know where offset is - in a string/comment or in normal code?
            // ignore cases where offset is in a block comment
            var starttype = GetStartType(document, linestart, offset);
            if (starttype != 0) return -1; // start position is in a comment/string

            var brackets = 1;

            while (offset < document.TextLength)
            {
                var ch = document.GetCharAt(offset);
                switch (ch)
                {
                    case '\r':
                    case '\n':
                        lineComment = false;
                        inChar = false;
                        if (!verbatim) inString = false;
                        break;
                    case '/':
                        if (blockComment)
                            if (document.GetCharAt(offset - 1) == '*')
                                blockComment = false;
                        if (!inString && !inChar && offset + 1 < document.TextLength)
                            if (!lineComment && document.GetCharAt(offset + 1) == '*')
                                blockComment = true;
                        break;
                    case '-':
                        if (!inString && !inChar && offset + 1 < document.TextLength)
                            if (!blockComment && document.GetCharAt(offset + 1) == '-')
                                lineComment = true;
                        break;
                    default:
                        if (ch == openBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment)) ++brackets;
                        }
                        else if (ch == closingBracket)
                        {
                            if (!(inString || inChar || lineComment || blockComment))
                            {
                                --brackets;
                                if (brackets == 0) return offset;
                            }
                        }

                        break;
                }

                ++offset;
            }

            return -1;
        }

        #endregion

        private int QuickSearchBracketBackward(TextDocument document, int offset, char openBracket, char closingBracket)
        {
            var brackets = -1;
            // first try "quick find" - find the matching bracket if there is no string/comment in the way
            for (var i = offset; i >= 0; --i)
            {
                var ch = document.GetCharAt(i);
                if (ch == openBracket)
                {
                    ++brackets;
                    if (brackets == 0) return i;
                }
                else if (ch == closingBracket)
                {
                    --brackets;
                }
                else if (ch == '"')
                {
                    break;
                }
                else if (ch == '\'')
                {
                    break;
                }
                else if (ch == '/' && i > 0)
                {
                    if (document.GetCharAt(i - 1) == '*') break;
                }
                else if (ch == '-' && i > 0)
                {
                    if (document.GetCharAt(i - 1) == '-') break;
                }
            }

            return -1;
        }

        private int QuickSearchBracketForward(TextDocument document, int offset, char openBracket, char closingBracket)
        {
            var brackets = 1;
            // try "quick find" - find the matching bracket if there is no string/comment in the way
            for (var i = offset; i < document.TextLength; ++i)
            {
                var ch = document.GetCharAt(i);
                if (ch == openBracket)
                {
                    ++brackets;
                }
                else if (ch == closingBracket)
                {
                    --brackets;
                    if (brackets == 0) return i;
                }
                else if (ch == '"')
                {
                    break;
                }
                else if (ch == '\'')
                {
                    break;
                }
                else if (ch == '-' && i > 0)
                {
                    if (document.GetCharAt(i - 1) == '-') break;
                }
                else if (ch == '*' && i > 0)
                {
                    if (document.GetCharAt(i - 1) == '/') break;
                }
            }

            return -1;
        }

        #region SearchBracket helper functions

        private static int ScanLineStart(TextDocument document, int offset)
        {
            for (var i = offset - 1; i > 0; --i)
                if (document.GetCharAt(i) == '\n')
                    return i + 1;
            return 0;
        }

        /// <summary>
        ///     Gets the type of code at offset.<br />
        ///     0 = Code,<br />
        ///     1 = Comment,<br />
        ///     2 = String<br />
        ///     Block comments and multiline strings are not supported.
        /// </summary>
        private static int GetStartType(TextDocument document, int linestart, int offset)
        {
            var inString = false;
            var inChar = false;
            var verbatim = false;
            var result = 0;
            for (var i = linestart; i < offset; i++)
                switch (document.GetCharAt(i))
                {
                    case '/':
                        if (!inString && !inChar && i + 1 < document.TextLength)
                            if (document.GetCharAt(i + 1) == '/')
                                result = 1;
                        break;
                    case '"':
                        if (!inChar)
                        {
                            if (inString && verbatim)
                            {
                                if (i + 1 < document.TextLength && document.GetCharAt(i + 1) == '"')
                                {
                                    ++i; // skip escaped quote
                                    inString = false; // let the string go on
                                }
                                else
                                {
                                    verbatim = false;
                                }
                            }
                            else if (!inString && i > 0 && document.GetCharAt(i - 1) == '@')
                            {
                                verbatim = true;
                            }

                            inString = !inString;
                        }

                        break;
                    case '\'':
                        if (!inString) inChar = !inChar;
                        break;
                    case '\\':
                        if (inString && !verbatim || inChar)
                            ++i; // skip next character
                        break;
                }

            return inString || inChar ? 2 : result;
        }

        #endregion
    }
}