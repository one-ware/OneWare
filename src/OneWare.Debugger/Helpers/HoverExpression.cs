namespace OneWare.Debugger.Helpers;

// Finds the expression to evaluate for a hovered identifier: the identifier itself plus any member
// access chain in front of it, so hovering "y" in "p->pos.y" evaluates "p->pos.y" like other IDEs.
public static class HoverExpression
{
    private static readonly HashSet<string> Keywords =
    [
        "auto", "bool", "break", "case", "char", "class", "const", "constexpr", "continue", "default", "delete",
        "do", "double", "else", "enum", "extern", "false", "float", "for", "goto", "if", "inline", "int", "long",
        "namespace", "new", "nullptr", "private", "protected", "public", "register", "return", "short", "signed",
        "sizeof", "static", "struct", "switch", "template", "this", "true", "typedef", "typename", "union",
        "unsigned", "using", "virtual", "void", "volatile", "while", "NULL"
    ];

    // Returns null if the offset is not on an identifier, or on a keyword.
    public static string? Extract(string line, int column)
    {
        if (column < 0 || column > line.Length) return null;

        var start = column;
        while (start > 0 && IsIdentifierChar(line[start - 1])) start--;

        var end = column;
        while (end < line.Length && IsIdentifierChar(line[end])) end++;

        if (start == end || char.IsDigit(line[start])) return null;
        if (Keywords.Contains(line[start..end])) return null;

        while (true)
        {
            int accessStart;
            if (start >= 1 && line[start - 1] == '.') accessStart = start - 1;
            else if (start >= 2 && line[start - 2] == '-' && line[start - 1] == '>') accessStart = start - 2;
            else break;

            var ownerStart = accessStart;
            while (ownerStart > 0 && IsIdentifierChar(line[ownerStart - 1])) ownerStart--;

            if (ownerStart == accessStart || char.IsDigit(line[ownerStart])) break;

            start = ownerStart;
        }

        return line[start..end];
    }

    // First identifier of an expression, e.g. "p" for "p->pos.y".
    public static string RootOf(string expression)
    {
        var end = 0;
        while (end < expression.Length && IsIdentifierChar(expression[end])) end++;
        return expression[..end];
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';
}
