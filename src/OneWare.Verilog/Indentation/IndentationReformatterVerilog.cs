using System.Globalization;
using System.Text;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Verilog.Indentation;

internal sealed class IndentationSettings
{
    public string IndentString { get; set; } = "\t";
    public bool LeaveEmptyLines { get; set; } = true;
}

/// <summary>
///     Verilog/SystemVerilog indentation reformatter.
///     Tracks begin/end, keyword blocks (module, function, task, case, etc.),
///     and one-liner control statements (if/else/for/while/always/initial/etc.).
/// </summary>
internal sealed class IndentationReformatterVerilog
{
    private Block _block;
    private Stack<Block> _blocks = new();

    // Comment / string state
    private bool _blockComment;
    private bool _lineComment;
    private bool _inString;
    private bool _escape;

    private char _lastRealChar;
    private StringBuilder _wordBuilder = new();

    // One-liner tracking
    private bool _blockOpenedThisLine;
    private string? _pendingOneLineKeyword;

    // Paren depth (for ignoring ';' inside for-loop headers etc.)
    private int _parenDepth;

    // Keywords that push a new indent block (matched by end<keyword>)
    private static readonly HashSet<string> BlockOpenKeywords = new(StringComparer.Ordinal)
    {
        "module", "macromodule", "primitive", "program",
        "function", "task",
        "case", "casex", "casez",
        "generate",
        "interface",
        "class", "package",
        "clocking", "covergroup",
        "property", "sequence", "checker",
        "config",
        "fork"
    };

    // Keywords that pop an indent block
    private static readonly HashSet<string> BlockCloseKeywords = new(StringComparer.Ordinal)
    {
        "end",
        "endmodule", "endmacromodule", "endprimitive", "endprogram",
        "endfunction", "endtask",
        "endcase",
        "endgenerate",
        "endinterface",
        "endclass", "endpackage",
        "endclocking", "endgroup",
        "endproperty", "endsequence", "endchecker",
        "endconfig",
        "join", "join_any", "join_none"
    };

    // Keywords that indent the next single line (one-liner) unless the line ends with 'begin'
    private static readonly HashSet<string> OneLineKeywords = new(StringComparer.Ordinal)
    {
        "if", "else", "for", "while", "forever", "repeat", "foreach",
        "always", "always_ff", "always_comb", "always_latch", "initial"
    };

    public void Reformat(IDocumentAccessor doc, IndentationSettings set)
    {
        Init();
        while (doc.MoveNext())
            Step(doc, set);
    }

    public void Init()
    {
        _wordBuilder = new StringBuilder();
        _blocks = new Stack<Block>();
        _block = new Block
        {
            InnerIndent = "",
            OuterIndent = "",
            Bracket = "",
            Continuation = false,
            LastWord = "",
            OneLineBlock = 0,
            PreviousOneLineBlock = 0,
            StartLine = 0
        };
        _blockComment = false;
        _lineComment = false;
        _inString = false;
        _escape = false;
        _lastRealChar = ' ';
        _blockOpenedThisLine = false;
        _pendingOneLineKeyword = null;
        _parenDepth = 0;
    }

    public void Step(IDocumentAccessor doc, IndentationSettings set)
    {
        var line = doc.Text;
        if (set.LeaveEmptyLines && line.Length == 0) return;
        line = line.TrimStart();

        if (line.Length == 0)
        {
            // Empty line: apply current inner indent + any pending one-liner extra
            if (_blockComment || _inString) return;
            var emptyIndent = BuildIndent(_block, set);
            if (doc.Text != emptyIndent)
                doc.Text = emptyIndent;
            return;
        }

        if (TrimEnd(doc))
            line = doc.Text.TrimStart();

        var oldBlock = _block;
        var startInComment = _blockComment;
        var startInString = _inString;

        _lineComment = false;
        _lastRealChar = '\n';
        _blockOpenedThisLine = false;
        _pendingOneLineKeyword = null;

        var c = ' ';
        var nextchar = line[0];

        for (var i = 0; i < line.Length; i++)
        {
            if (_lineComment) break;

            var lastchar = c;
            c = nextchar;
            nextchar = i + 1 < line.Length ? line[i + 1] : '\n';

            if (_escape)
            {
                _escape = false;
                continue;
            }

            // Track comment / string state
            switch (c)
            {
                case '/':
                    if (_blockComment && lastchar == '*')
                    {
                        _blockComment = false;
                    }
                    else if (!_inString && !_blockComment)
                    {
                        if (nextchar == '/') _lineComment = true;
                        else if (nextchar == '*') _blockComment = true;
                    }
                    break;
                case '"':
                    if (!_lineComment && !_blockComment)
                        _inString = !_inString;
                    break;
                case '\\':
                    if (_inString) _escape = true;
                    break;
            }

            if (_lineComment || _blockComment || _inString)
            {
                if (_wordBuilder.Length > 0)
                {
                    _block.LastWord = _wordBuilder.ToString();
                    _wordBuilder.Length = 0;
                }
                continue;
            }

            // Build identifier / keyword words
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                _wordBuilder.Append(c);
            }
            else
            {
                if (_wordBuilder.Length > 0)
                {
                    ProcessWord(_wordBuilder.ToString(), doc, set);
                    _wordBuilder.Length = 0;
                }

                // Track paren depth (without creating indent blocks for them)
                switch (c)
                {
                    case '(':
                        _parenDepth++;
                        break;
                    case ')':
                        if (_parenDepth > 0) _parenDepth--;
                        break;
                    case ';':
                        // End of statement at top-level paren depth
                        if (_parenDepth == 0)
                        {
                            if (_block.OneLineBlock > 0)
                                _block.OneLineBlock--;
                            _block.Continuation = false;
                        }
                        break;
                }
            }

            if (!char.IsWhiteSpace(c))
                _lastRealChar = c;
        }

        // Flush remaining word at end of line
        if (_wordBuilder.Length > 0)
        {
            ProcessWord(_wordBuilder.ToString(), doc, set);
            _wordBuilder.Length = 0;
        }

        // Check for pending one-liner: a control keyword was seen, no block was opened
        if (_pendingOneLineKeyword != null && !_blockOpenedThisLine)
            _block.OneLineBlock++;

        if (startInString || startInComment) return;

        // Determine indentation for this line
        var firstWord = GetFirstWord(line);
        var isCloser = BlockCloseKeywords.Contains(firstWord);
        var isElse = firstWord == "else";

        string indent;
        if (isCloser || isElse)
        {
            indent = oldBlock.OuterIndent;
            oldBlock.ResetOneLineBlock();
            oldBlock.Continuation = false;
        }
        else
        {
            indent = BuildIndent(oldBlock, set);
        }

        if (!doc.IsReadOnly)
        {
            var leadingLen = doc.Text.Length - line.Length;
            if (leadingLen != indent.Length ||
                !doc.Text.StartsWith(indent, StringComparison.Ordinal))
                doc.Text = indent + line;
        }
    }

    private void ProcessWord(string word, IDocumentAccessor doc, IndentationSettings set)
    {
        _block.LastWord = word;

        if (word == "begin" || BlockOpenKeywords.Contains(word))
        {
            // Opening a new block: cancel pending one-liner (begin supersedes it)
            _pendingOneLineKeyword = null;
            _blockOpenedThisLine = true;
            _block.ResetOneLineBlock();
            _block.OneLineBlock = 0;
            _blocks.Push(_block);
            _block.StartLine = doc.LineNumber;
            _block.Indent(set);
            _block.Bracket = word;
        }
        else if (BlockCloseKeywords.Contains(word))
        {
            if (_blocks.Count > 0)
            {
                _block = _blocks.Pop();
                _block.Continuation = false;
                _block.ResetOneLineBlock();
                _block.OneLineBlock = 0;
            }
        }
        else if (OneLineKeywords.Contains(word))
        {
            // Record the most recent one-liner keyword on this line; will be confirmed at end of line
            _pendingOneLineKeyword = word;
        }
    }

    private static string BuildIndent(Block block, IndentationSettings set)
    {
        if (block.OneLineBlock <= 0) return block.InnerIndent;
        var sb = new StringBuilder(block.InnerIndent);
        for (var i = 0; i < block.OneLineBlock; i++)
            sb.Append(set.IndentString);
        return sb.ToString();
    }

    private static string GetFirstWord(string line)
    {
        var trimmed = line.TrimStart();
        var i = 0;
        while (i < trimmed.Length && (char.IsLetterOrDigit(trimmed[i]) || trimmed[i] == '_'))
            i++;
        return i > 0 ? trimmed[..i] : string.Empty;
    }

    private static bool TrimEnd(IDocumentAccessor doc)
    {
        var line = doc.Text;
        if (line.Length == 0 || !char.IsWhiteSpace(line[^1])) return false;
        if (line.EndsWith("// ", StringComparison.Ordinal) || line.EndsWith("* ", StringComparison.Ordinal))
            return false;
        doc.Text = line.TrimEnd();
        return true;
    }

    /// <summary>
    ///     Tracks the state of indentation within a block scope.
    /// </summary>
    private struct Block
    {
        /// <summary>The indentation outside of the block (used for closing keywords).</summary>
        public string OuterIndent;

        /// <summary>The indentation inside the block (used for body lines).</summary>
        public string InnerIndent;

        /// <summary>The last identifier word seen inside this block.</summary>
        public string LastWord;

        /// <summary>The keyword/bracket that opened this block.</summary>
        public string Bracket;

        /// <summary>Whether there is a line continuation inside this block.</summary>
        public bool Continuation;

        /// <summary>Count of pending one-liner blocks (if/else/for without begin).</summary>
        public int OneLineBlock;

        /// <summary>Previous value of OneLineBlock before it was reset.</summary>
        public int PreviousOneLineBlock;

        /// <summary>Line number where this block started.</summary>
        public int StartLine;

        public void ResetOneLineBlock()
        {
            PreviousOneLineBlock = OneLineBlock;
            OneLineBlock = 0;
        }

        public void Indent(IndentationSettings set)
        {
            OuterIndent = InnerIndent;
            InnerIndent += set.IndentString;
            Continuation = false;
            ResetOneLineBlock();
            LastWord = "";
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "[Block StartLine={0}, Bracket='{1}', LastWord='{2}', OneLineBlock={3}]",
                StartLine, Bracket, LastWord, OneLineBlock);
        }
    }
}

