using System.Text;

namespace OneWare.Debugger.Helpers;

public sealed class GdbOutputFormatter
{
    private readonly StringBuilder _pending = new();
    private readonly object _lock = new();

    public IReadOnlyList<string> Accept(string? rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
            return [];

        if (!TryReadStream(rawLine, out var payload))
        {
            var line = FormatRecord(rawLine);
            return line == null ? [] : [line];
        }

        lock (_lock)
        {
            _pending.Append(payload);
            return ReadCompleteLines();
        }
    }

    public IReadOnlyList<string> Flush()
    {
        lock (_lock)
        {
            if (_pending.Length == 0)
                return [];

            var rest = _pending.ToString().TrimEnd('\r', '\n');
            _pending.Clear();

            return rest.Length > 0 ? [rest] : [];
        }
    }

    private static string? FormatRecord(string rawLine)
    {
        var line = rawLine.Trim();

        if (line == "(gdb)")
            return line;

        if (line == "^done")
            return null;

        if (line.StartsWith('='))
            return null;

        if (line.StartsWith("^error,msg=", StringComparison.Ordinal))
            return $"error: {Unquote(line["^error,msg=".Length..])}";

        return line;
    }

    private static bool TryReadStream(string rawLine, out string payload)
    {
        payload = string.Empty;

        var line = rawLine.Trim();

        if (line.Length < 2 || line[0] is not ('~' or '@' or '&'))
            return false;

        payload = Unquote(line[1..]);
        return true;
    }

    private List<string> ReadCompleteLines()
    {
        var lines = new List<string>();

        while (true)
        {
            var newline = _pending.ToString().IndexOf('\n');

            if (newline < 0)
                break;

            var line = _pending.ToString(0, newline).TrimEnd('\r');
            _pending.Remove(0, newline + 1);

            if (line.Length > 0)
                lines.Add(line);
        }

        return lines;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];

        var result = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                result.Append(value[i]);
                continue;
            }

            i++;

            result.Append(value[i] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '"' => '"',
                '\\' => '\\',
                _ => value[i]
            });
        }

        return result.ToString();
    }
}