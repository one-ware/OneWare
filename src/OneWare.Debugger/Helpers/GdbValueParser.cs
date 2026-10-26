using System.Text.RegularExpressions;
using OneWare.Debugger.Models;

namespace OneWare.Debugger.Helpers;

// Turns a value as GDB prints it - e.g. {a = 1, p = 0x4011a6 <main+4>, s = {x = 2, y = 3}, arr = {1, 2, 3}} -
// into a tree. Unnamed members (array elements) are numbered [0], [1], ... Anything that cannot be
// read as a structure stays a single scalar, so the user always sees GDB's original text.
public static partial class GdbValueParser
{
    private const int SummaryLength = 120;

    public static DebugValueNode Parse(string name, string value)
    {
        var text = value.Trim();
        var position = 0;

        if (text.StartsWith('{') && ParseValue(text, ref position, name) is { } node)
        {
            SkipWhitespace(text, ref position);
            if (position == text.Length) return node;
        }

        return new DebugValueNode(name, text);
    }

    private static DebugValueNode? ParseValue(string text, ref int position, string name)
    {
        SkipWhitespace(text, ref position);

        if (position < text.Length && text[position] == '{')
        {
            var start = position;
            if (ParseComposite(text, ref position, name) is { } composite)
            {
                // "{int (int)} 0x401126 <f>": a type in braces in front of a scalar (function values).
                SkipWhitespace(text, ref position);
                if (position >= text.Length || text[position] is ',' or '}') return composite;
            }

            position = start;
        }

        var scalar = ReadScalar(text, ref position);
        return new DebugValueNode(name, scalar);
    }

    private static DebugValueNode? ParseComposite(string text, ref int position, string name)
    {
        var start = position;
        position++; // '{'

        var children = new List<DebugValueNode>();
        var index = 0;

        while (true)
        {
            SkipWhitespace(text, ref position);
            if (position >= text.Length) return null;

            if (text[position] == '}')
            {
                position++;
                break;
            }

            if (text.AsSpan(position).StartsWith("...}"))
            {
                // Nested beyond GDB's print depth.
                position += 4;
                return new DebugValueNode(name, "{...}");
            }

            string? memberName = null;
            var match = MemberNameRegex().Match(text, position);
            if (match.Success)
            {
                memberName = match.Groups["name"].Value;
                position += match.Length;
            }

            var child = ParseValue(text, ref position, memberName ?? $"[{index}]");
            if (child == null) return null;

            // Char arrays print as "hi", '\000' <repeats 18 times>: the unnamed piece belongs to the
            // previous member.
            if (memberName == null && children.Count > 0 && IsNamed(children[^1]) && child.Children.Count == 0)
                children[^1] = new DebugValueNode(children[^1].Name, $"{children[^1].Value}, {child.Value}");
            else
            {
                children.Add(child);
                index++;
            }

            SkipWhitespace(text, ref position);
            if (position >= text.Length) return null;

            if (text[position] == ',')
            {
                position++;
                continue;
            }

            if (text[position] != '}') return null;
        }

        return new DebugValueNode(name, Summarize(text[start..position]), children);
    }

    private static bool IsNamed(DebugValueNode node) => !node.Name.StartsWith('[');

    // Reads up to the next ',' or '}' that is not inside quotes, <...> or (...).
    private static string ReadScalar(string text, ref int position)
    {
        var start = position;
        var depth = 0;

        while (position < text.Length)
        {
            var c = text[position];

            if (c is '"' or '\'')
            {
                SkipQuoted(text, ref position, c);
                continue;
            }

            if (c is '<' or '(' or '[' or '{') depth++;
            else if (c is '>' or ')' or ']' or '}' && depth > 0) depth--;
            else if (c is ',' or '}' && depth == 0) break;

            position++;
        }

        return text[start..position].Trim();
    }

    private static void SkipQuoted(string text, ref int position, char quote)
    {
        position++;
        while (position < text.Length)
        {
            if (text[position] == '\\')
            {
                position += 2;
                continue;
            }

            if (text[position++] == quote) return;
        }
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
    }

    private static string Summarize(string text)
    {
        return text.Length <= SummaryLength ? text : text[..(SummaryLength - 1)] + "…";
    }

    // "name = ", "[3] = " (designated array index) or "<Base> = " (C++ base class).
    [GeneratedRegex(@"\G(?<name>[A-Za-z_$][\w$]*|\[[^\]]+\]|<[^>]+>) = ")]
    private static partial Regex MemberNameRegex();
}
