namespace OneWare.Core.Extensions.TextMate;

public class TextMateLanguage(string id, string grammarPath, params string[] extensions)
{
    public string Id { get; } = id;

    public string GrammarPath { get; } = grammarPath;

    public IEnumerable<string> Extensions { get; } = extensions;
}