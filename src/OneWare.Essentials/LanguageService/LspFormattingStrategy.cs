using AvaloniaEdit.Document;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Essentials.LanguageService;

public class LspFormattingStrategy(ILanguageService languageService, string filePath) : IFormattingStrategy
{
    public void Format(TextDocument document)
    {
        _ = FormatAsync();
    }

    private async Task FormatAsync()
    {
        var edits = await languageService.RequestFormattingAsync(filePath);
        if (edits is not null) languageService.ApplyContainer(filePath, edits);
    }
}
