using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Verilog.Indentation;

/// <summary>
///     Synchronous indentation strategy for Verilog / SystemVerilog.
///     Handles begin/end, keyword blocks (module, function, task, case, generate, …),
///     fork/join, and one-liner control statements (if/else/for/while/always/…).
/// </summary>
public class VerilogIndentationStrategy : DefaultIndentationStrategy
{
    private readonly TextEditorOptions? _options;
    private string _indentationString = "    ";

    public VerilogIndentationStrategy()
    {
    }

    /// <summary>
    ///     Creates a new VerilogIndentationStrategy and initialises the indentation
    ///     string from the text-editor options.
    /// </summary>
    public VerilogIndentationStrategy(TextEditorOptions options)
    {
        _options = options;
    }

    /// <summary>Gets/Sets the string used for one level of indentation.</summary>
    public string IndentationString
    {
        get => _options?.IndentationString ?? _indentationString;
        set
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Indentation string must not be null or empty");
            _indentationString = value;
        }
    }

    /// <summary>
    ///     Runs the Verilog reformatter over the given accessor range.
    /// </summary>
    public void Indent(IDocumentAccessor document, bool keepEmptyLines)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));

        var settings = new IndentationSettings
        {
            IndentString = IndentationString,
            LeaveEmptyLines = keepEmptyLines
        };

        var r = new IndentationReformatterVerilog();
        r.Reformat(document, settings);
    }

    /// <inheritdoc />
    public override void IndentLine(TextDocument document, DocumentLine line)
    {
        var lineNr = line.LineNumber;
        var acc = new TextDocumentAccessor(document, lineNr, lineNr);
        Indent(acc, false);

        // Fall back to default (copy leading whitespace from previous line) when
        // the reformatter produced an empty result — e.g. inside block comments.
        if (acc.Text.Length == 0)
            base.IndentLine(document, line);
    }

    /// <inheritdoc />
    public override void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        Indent(new TextDocumentAccessor(document, beginLine, endLine), true);
    }
}

