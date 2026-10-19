using System.Text;

namespace OneWare.TerminalManager.Models;

/// <summary>
/// Bounded capture of command output that keeps the beginning and the end. When the output exceeds the
/// limit, the middle is dropped and replaced by a marker, so neither the first lines (what the command
/// started with) nor the last lines (how it ended) are lost.
/// Lengths and line counts treat the pty's "\r\n" as the single "\n" the program wrote.
/// </summary>
internal sealed class HeadTailOutputBuffer(int maxChars)
{
    private readonly int _headLimit = maxChars / 2;
    private readonly int _tailLimit = maxChars - maxChars / 2;
    private readonly StringBuilder _head = new();
    private readonly StringBuilder _tail = new();
    private long _rawLength;
    private long _newlines;
    private long _crlfs;
    private bool _lastWasCr;

    /// <summary>Characters appended since the last <see cref="Clear" />, including dropped ones.</summary>
    public long TotalLength => _rawLength - _crlfs;

    /// <summary>Lines appended since the last <see cref="Clear" />; an unterminated last line counts.</summary>
    public long LineCount => _newlines + (_rawLength > 0 && !EndsWithNewline ? 1 : 0);

    public long OmittedLength => _rawLength - _head.Length - VisibleTailLength;

    private int VisibleTailLength => Math.Min(_tail.Length, _tailLimit);

    private bool EndsWithNewline => (_tail.Length > 0 ? _tail[^1] : _head.Length > 0 ? _head[^1] : '\0') == '\n';

    public void Append(string text)
    {
        foreach (var c in text)
        {
            if (c == '\n')
            {
                _newlines++;
                if (_lastWasCr) _crlfs++;
            }

            _lastWasCr = c == '\r';
        }

        _rawLength += text.Length;

        var headSpace = _headLimit - _head.Length;
        if (headSpace > 0)
        {
            var toHead = Math.Min(headSpace, text.Length);
            _head.Append(text, 0, toHead);
            if (toHead == text.Length) return;
            text = text[toHead..];
        }

        _tail.Append(text);
        // Trimming only once the tail holds twice its limit keeps appends amortized O(1).
        if (_tail.Length > _tailLimit * 2)
            _tail.Remove(0, _tail.Length - _tailLimit);
    }

    public void Clear()
    {
        _head.Clear();
        _tail.Clear();
        _rawLength = 0;
        _newlines = 0;
        _crlfs = 0;
        _lastWasCr = false;
    }

    public override string ToString()
    {
        var tailStart = _tail.Length - VisibleTailLength;
        var tail = _tail.ToString(tailStart, _tail.Length - tailStart);
        if (OmittedLength == 0) return _head + tail;

        var head = _head.ToString();
        var (headNewlines, headCrlfs) = CountLineBreaks(head);
        var (tailNewlines, tailCrlfs) = CountLineBreaks(tail);
        // A "\r\n" split exactly at a boundary is not counted on either side; the difference is negligible.
        var omittedChars = OmittedLength - (_crlfs - headCrlfs - tailCrlfs);
        var omittedLines = _newlines - headNewlines - tailNewlines;

        return $"{head}\n[... {omittedLines} lines ({omittedChars} characters) of output omitted ...]\n{tail}";
    }

    private static (long Newlines, long Crlfs) CountLineBreaks(string text)
    {
        long newlines = 0, crlfs = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            newlines++;
            if (i > 0 && text[i - 1] == '\r') crlfs++;
        }

        return (newlines, crlfs);
    }
}
