using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;

namespace OneWare.Python;

public class PythonIndentationStrategy(TextEditorOptions options) : DefaultIndentationStrategy
{
    public override void IndentLine(TextDocument document, DocumentLine line)
    {
        if (line.PreviousLine is not { } previousLine)
            return;

        var previousText = document.GetText(previousLine);
        var indentation = previousText[..GetIndentationLength(previousText)];
        if (EndsWithBlockColon(document.GetText(0, previousLine.EndOffset)))
            indentation += options.IndentationString;

        var text = document.GetText(line);
        document.Replace(line.Offset, GetIndentationLength(text), indentation);
    }

    public override void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        // Reindenting existing Python code can change its meaning. This strategy only handles newlines.
    }

    private static int GetIndentationLength(string text)
    {
        var length = 0;
        while (length < text.Length && text[length] is ' ' or '\t')
            length++;
        return length;
    }

    private static bool EndsWithBlockColon(string text)
    {
        var quote = '\0';
        var tripleQuoted = false;
        var bracketDepth = 0;
        var lastCodeCharacter = '\0';

        // Scan preceding lines too so colons inside multiline strings and brackets are ignored.
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (quote != '\0')
            {
                if (character == '\\')
                {
                    i++;
                }
                else if (character == quote &&
                         (!tripleQuoted || i + 2 < text.Length && text[i + 1] == quote && text[i + 2] == quote))
                {
                    if (tripleQuoted)
                        i += 2;
                    quote = '\0';
                    lastCodeCharacter = character;
                }
                else if (!tripleQuoted && character is '\r' or '\n')
                {
                    quote = '\0';
                    lastCodeCharacter = '\0';
                }

                continue;
            }

            if (character == '#')
            {
                while (i + 1 < text.Length && text[i + 1] is not ('\r' or '\n'))
                    i++;
            }
            else if (character is '\'' or '"')
            {
                quote = character;
                tripleQuoted = i + 2 < text.Length && text[i + 1] == quote && text[i + 2] == quote;
                if (tripleQuoted)
                    i += 2;
                lastCodeCharacter = character;
            }
            else if (character is '\r' or '\n')
            {
                lastCodeCharacter = '\0';
            }
            else if (!char.IsWhiteSpace(character))
            {
                if (character is '(' or '[' or '{')
                    bracketDepth++;
                else if (character is ')' or ']' or '}')
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                lastCodeCharacter = character;
            }
        }

        return quote == '\0' && bracketDepth == 0 && lastCodeCharacter == ':';
    }
}
