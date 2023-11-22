using System.Globalization;
using System.Text;
using OneWare.SDK.EditorExtensions;

namespace OneWare.Vhdl.Indentation
{
    internal sealed class IndentationSettings
    {
        public string IndentString { get; set; } = "\t";

        public bool LeaveEmptyLines { get; set; } = true;
    }
    
    internal sealed class IndentationReformatterVhdl
    {
        private Block _block; // block is the current block
        private bool _blockComment;
        private Stack<Block> _blocks = new(); // blocks contains all blocks outside of the current
        private bool _escape;
        private bool _inChar;

        private bool _inString;

        private char _lastRealChar; // last non-comment char

        private bool _lineComment;
        private bool _verbatim;

        private StringBuilder _wordBuilder = new();
        
        public void Reformat(IDocumentAccessor doc, IndentationSettings set)
        {
            Init();

            while (doc.MoveNext()) Step(doc, set);
        }

        public void Init()
        {
            _wordBuilder = new StringBuilder();
            _blocks = new Stack<Block>();
            _block = new Block
            {
                InnerIndent = "",
                OuterIndent = "",
                Bracket = "{",
                Continuation = false,
                LastWord = "",
                OneLineBlock = 0,
                PreviousOneLineBlock = 0,
                StartLine = 0
            };

            _inString = false;
            _inChar = false;
            _verbatim = false;
            _escape = false;

            _lineComment = false;
            _blockComment = false;

            _lastRealChar = ' '; // last non-comment char
        }

        public void Step(IDocumentAccessor doc, IndentationSettings set)
        {
            var line = doc.Text;
            if (set.LeaveEmptyLines && line.Length == 0) return; // leave empty lines empty
            line = line.TrimStart();

            var indent = new StringBuilder();
            if (line.Length == 0)
            {
                // Special treatment for empty lines:
                if (_blockComment || _inString && _verbatim)
                    return;
                indent.Append(_block.InnerIndent);
                indent.Append(Repeat(set.IndentString, _block.OneLineBlock));

                if (doc.Text != indent.ToString())
                    doc.Text = indent.ToString();
                return;
            }

            if (TrimEnd(doc))
                line = doc.Text.TrimStart();

            var oldBlock = _block;
            var startInComment = _blockComment;
            var startInString = _inString && _verbatim;

            #region Parse char by char

            _lineComment = false;
            _inChar = false;
            _escape = false;


            if (!_verbatim) _inString = false;

            _lastRealChar = '\n';

            var c = ' ';
            var nextchar = line[0];
            var lineText = " ";
            for (var i = 0; i < line.Length; i++)
            {
                if (_lineComment) break; // cancel parsing current line

                var lastchar = c;
                c = nextchar;
                nextchar = i + 1 < line.Length ? line[i + 1] : '\n';
                lineText += c;
                if (i == line.Length - 1) lineText += " ";

                if (_escape)
                {
                    _escape = false;
                    continue;
                }

                #region Check for comment/string chars

                switch (c)
                {
                    case '/':
                        if (_blockComment && lastchar == '*')
                        {
                            _blockComment = false;
                            if (i < 2) startInComment = true;
                        }

                        if (!_inString && !_inChar)
                            if (!_lineComment && nextchar == '*')
                                _blockComment = true;
                        break;
                    case '-':
                        if (!_inString && !_inChar && !_blockComment && nextchar == '-')
                            _lineComment = true;
                        break;
                    case '>':
                        if (!_inString && !_inChar && !_blockComment && nextchar == '>')
                            _lineComment = true;
                        break;
                    case '<':
                        if (!_inString && !_inChar && !_blockComment && nextchar == '<')
                            _lineComment = true;
                        break;
                    case '=':
                        if (!_inString && !_inChar && !_blockComment && nextchar == '=')
                            _lineComment = true;
                        break;
                    case '#':
                        if (!(_inChar || _blockComment || _inString))
                            _lineComment = true;
                        break;
                    case '"':
                        if (!(_inChar || _lineComment || _blockComment))
                        {
                            _inString = !_inString;
                            if (!_inString && _verbatim)
                            {
                                if (nextchar == '"')
                                {
                                    _escape = true; // skip escaped quote
                                    _inString = true;
                                }
                                else
                                {
                                    _verbatim = false;
                                }
                            }
                            else if (_inString && lastchar == '@')
                            {
                                _verbatim = true;
                            }
                        }

                        break;
                    case '\\':
                        if (_inString && !_verbatim || _inChar)
                            _escape = true; // skip next character
                        break;
                }

                #endregion

                if (_lineComment || _blockComment || _inString || _inChar)
                {
                    if (_wordBuilder.Length > 0) _block.LastWord = _wordBuilder.ToString();
                    _wordBuilder.Length = 0;
                    if (i < 2) startInComment = true;
                    continue;
                }

                if (!char.IsWhiteSpace(c) && c != '[' && c != '/')
                    if (_block.Bracket == "{" || _block.Bracket == "(" || _block.Bracket == "BEGIN" ||
                        _block.Bracket == "IS" || _block.Bracket == "IF")
                        _block.Continuation = true;

                if (char.IsLetterOrDigit(c))
                {
                    _wordBuilder.Append(c);
                }
                else
                {
                    if (_wordBuilder.Length > 0)
                        _block.LastWord = _wordBuilder.ToString();
                    _wordBuilder.Length = 0;
                }

                #region Push/Pop the blocks

                if (lineText.EndsWith(" BEGIN ", StringComparison.OrdinalIgnoreCase))
                {
                    if (_block.Bracket is "IS" or "GENERATE")
                    {
                        if (_blocks.Count == 0) break;
                        _block = _blocks.Pop();
                        _block.Continuation = false;
                    }

                    _block.ResetOneLineBlock();
                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "BEGIN";
                }

                if (lineText.EndsWith(" IS ", StringComparison.OrdinalIgnoreCase) && !line.Contains(";"))
                {
                    _block.ResetOneLineBlock();
                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "IS";
                }

                if (lineText.EndsWith(" WHEN ", StringComparison.OrdinalIgnoreCase) && line.Contains("=>"))
                {
                    _block.ResetOneLineBlock();
                    if (_block.Bracket == "WHEN")
                    {
                        if (_blocks.Count == 0) break;
                        _block = _blocks.Pop();
                    }

                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "WHEN";
                }

                if (lineText.EndsWith(" LOOP ", StringComparison.OrdinalIgnoreCase) && !line.Contains(";"))
                {
                    _block.ResetOneLineBlock();
                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "LOOP";
                }

                if (lineText.EndsWith(" GENERATE ", StringComparison.OrdinalIgnoreCase) && !line.Contains(";"))
                {
                    _block.ResetOneLineBlock();
                    if(_block.Bracket == "ELSE")
                    {
                        if (_blocks.Count == 0) break;
                        _block = _blocks.Pop();
                    }
                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "GENERATE";
                }
                else if (lineText.EndsWith(" THEN ", StringComparison.OrdinalIgnoreCase))
                {
                    _block.ResetOneLineBlock();
                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "IF";
                }
                else if (lineText.EndsWith(" END ", StringComparison.OrdinalIgnoreCase) ||
                         lineText.EndsWith(" END; ", StringComparison.OrdinalIgnoreCase))
                {
                    //while (_block.Bracket != "BEGIN" || _block.Bracket != "IF" || _block.Bracket != "IS")
                    //{
                    //    if (_blocks.Count == 0) break;
                    //    _block = _blocks.Pop();
                    //}
                    if (_block.Bracket == "WHEN")
                    {
                        if (_blocks.Count == 0) break;
                        _block = _blocks.Pop();
                        oldBlock = _block;
                    }

                    if (_blocks.Count == 0) break;
                    _block = _blocks.Pop();
                    _block.Continuation = false;
                    _block.ResetOneLineBlock();
                }
                else if (lineText.EndsWith(" ELSE ", StringComparison.OrdinalIgnoreCase))
                {
                    if (_blocks.Count == 0) break;
                    _block = _blocks.Pop();
                    _block.Continuation = false;
                    _block.ResetOneLineBlock();
                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "ELSE";
                }
                else if (lineText.EndsWith(" ELSIF ", StringComparison.OrdinalIgnoreCase))
                {
                    if (_blocks.Count == 0) break;
                    _block = _blocks.Pop();
                    _block.Continuation = false;
                    _block.ResetOneLineBlock();
                }
                else if (c == '(')
                {
                    _block.ResetOneLineBlock();
                    _blocks.Push(_block);
                    _block.StartLine = doc.LineNumber;
                    _block.Indent(set);
                    _block.Bracket = "(";
                }
                else if (c == ')')
                {
                    while (_block.Bracket != "(")
                    {
                        if (_blocks.Count == 0) break;
                        _block = _blocks.Pop();
                    }

                    if (_blocks.Count == 0) break;
                    _block = _blocks.Pop();
                    _block.Continuation = false;
                    _block.ResetOneLineBlock();
                }

                if (!char.IsWhiteSpace(c))
                    // register this char as last char
                    _lastRealChar = c;

                #endregion
            }

            #endregion

            if (_wordBuilder.Length > 0) _block.LastWord = _wordBuilder.ToString();
            _wordBuilder.Length = 0;

            if (startInString) return;
            if (startInComment) return; //&& line[0] != '*') return;
            if (doc.Text.StartsWith("//\t", StringComparison.Ordinal) || doc.Text == "//")
                return;

            if (line[0] == '}' || line[0] == ')'
                               || lineText.StartsWith(" BEGIN ", StringComparison.OrdinalIgnoreCase) &&
                               oldBlock.Bracket == "IS"
                               || lineText.StartsWith(" WHEN ", StringComparison.OrdinalIgnoreCase) &&
                               oldBlock.Bracket == "WHEN"
                               || lineText.StartsWith(" END ", StringComparison.OrdinalIgnoreCase)
                               || lineText.StartsWith(" END; ", StringComparison.OrdinalIgnoreCase)
                               //|| lineText.StartsWith(" THEN ", StringComparison.OrdinalIgnoreCase)
                               || lineText.StartsWith(" ELSE ", StringComparison.OrdinalIgnoreCase)
                               || lineText.StartsWith(" ELSIF ", StringComparison.OrdinalIgnoreCase))
            {
                indent.Append(oldBlock.OuterIndent);
                oldBlock.ResetOneLineBlock();
                oldBlock.Continuation = false;
            }
            else
            {
                indent.Append(oldBlock.InnerIndent);
            }

            if (doc.IsReadOnly)
            {
                // We can't change the current line, but we should accept the existing
                // indentation if possible (=if the current statement is not a multiline
                // statement).
                if (!oldBlock.Continuation && oldBlock.OneLineBlock == 0 &&
                    oldBlock.StartLine == _block.StartLine &&
                    _block.StartLine < doc.LineNumber && _lastRealChar != ':')
                {
                    // use indent StringBuilder to get the indentation of the current line
                    indent.Length = 0;
                    line = doc.Text; // get untrimmed line
                    foreach (var t in line)
                    {
                        if (!char.IsWhiteSpace(t))
                            break;
                        indent.Append(t);
                    }

                    // /* */ multiline comments have an extra space - do not count it
                    // for the block's indentation.
                    if (startInComment && indent.Length > 0 && indent[indent.Length - 1] == ' ') indent.Length -= 1;
                    _block.InnerIndent = indent.ToString();
                }

                return;
            }


            // this is only for blockcomment lines starting with *,
            // all others keep their old indentation
            if (startInComment)
                indent.Append(' ');

            //if (_lineComment && _wordBuilder.Length == 0) return; //Do not change comments #469

            if (indent.Length != doc.Text.Length - line.Length ||
                !doc.Text.StartsWith(indent.ToString(), StringComparison.Ordinal) ||
                char.IsWhiteSpace(doc.Text[indent.Length]))
                doc.Text = indent + line;
        }

        private static string Repeat(string text, int count)
        {
            if (count == 0)
                return string.Empty;
            if (count == 1)
                return text;
            var b = new StringBuilder(text.Length * count);
            for (var i = 0; i < count; i++)
                b.Append(text);
            return b.ToString();
        }

        private static bool TrimEnd(IDocumentAccessor doc)
        {
            var line = doc.Text;
            if (!char.IsWhiteSpace(line[^1])) return false;

            // one space after an empty comment is allowed
            if (line.EndsWith("// ", StringComparison.Ordinal) || line.EndsWith("* ", StringComparison.Ordinal))
                return false;

            doc.Text = line.TrimEnd();
            return true;
        }

        /// <summary>
        ///     An indentation block. Tracks the state of the indentation.
        /// </summary>
        private struct Block
        {
            /// <summary>
            ///     The indentation outside of the block.
            /// </summary>
            public string OuterIndent;

            /// <summary>
            ///     The indentation inside the block.
            /// </summary>
            public string InnerIndent;

            /// <summary>
            ///     The last word that was seen inside this block.
            ///     Because parenthesis open a sub-block and thus don't change their parent's LastWord,
            ///     this property can be used to identify the type of block statement (if, while, switch)
            ///     at the position of the '{'.
            /// </summary>
            public string LastWord;

            /// <summary>
            ///     The type of bracket that opened this block (, [ or {
            /// </summary>
            public string Bracket;

            /// <summary>
            ///     Gets whether there's currently a line continuation going on inside this block.
            /// </summary>
            public bool Continuation;

            /// <summary>
            ///     Gets whether there's currently a 'one-line-block' going on. 'one-line-blocks' occur
            ///     with if statements that don't use '{}'. They are not represented by a Block instance on
            ///     the stack, but are instead handled similar to line continuations.
            ///     This property is an integer because there might be multiple nested one-line-blocks.
            ///     As soon as there is a finished statement, OneLineBlock is reset to 0.
            /// </summary>
            public int OneLineBlock;

            /// <summary>
            ///     The previous value of one-line-block before it was reset.
            ///     Used to restore the indentation of 'else' to the correct level.
            /// </summary>
            public int PreviousOneLineBlock;

            public void ResetOneLineBlock()
            {
                PreviousOneLineBlock = OneLineBlock;
                OneLineBlock = 0;
            }

            /// <summary>
            ///     Gets the line number where this block started.
            /// </summary>
            public int StartLine;

            public void Indent(IndentationSettings set)
            {
                Indent(set.IndentString);
            }

            public void Indent(string indentationString)
            {
                OuterIndent = InnerIndent;
                InnerIndent += indentationString;
                Continuation = false;
                ResetOneLineBlock();
                LastWord = "";
            }

            public override string ToString()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "[Block StartLine={0}, LastWord='{1}', Continuation={2}, OneLineBlock={3}, PreviousOneLineBlock={4}]",
                    StartLine, LastWord, Continuation, OneLineBlock, PreviousOneLineBlock);
            }
        }
    }
}