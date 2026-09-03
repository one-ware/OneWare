using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.LanguageService;

public class LspFoldingStrategy : IFoldingStrategy
{
    private readonly List<FoldingEntry> _foldings = new();
    private readonly LanguageServiceLsp _languageService;

    private readonly string _filePath;

    /// <summary>
    ///     Logic how code collapsing should work
    ///     Works but could be better ;)
    /// </summary>
    public LspFoldingStrategy(LanguageServiceLsp ls, string filePath)
    {
        _languageService = ls;
        _filePath = filePath;
    }

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        _ = UpdateFoldingsAsync(manager, document);
    }

    public async Task UpdateFoldingsAsync(FoldingManager manager, TextDocument document)
    {
        try
        {
            var beforeFolding = DateTime.Now.TimeOfDay;
            var newFoldings = await CreateNewFoldingsAsync(document);
            manager.UpdateFoldings(newFoldings, -1);

            //ContainerLocator.Container.Resolve<ILogger>()?.Log("Updated foldings after: " + (DateTime.Now.TimeOfDay - beforeFolding).Milliseconds + "ms", ConsoleColor.DarkGray);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    public async Task<IEnumerable<NewFolding>> CreateNewFoldingsAsync(TextDocument document)
    {
        var l = new List<NewFolding>();
        if (!_languageService.IsLanguageServiceReady) return l;
        var f = await _languageService.RequestFoldingsAsync(_filePath);
        if (f is not null)
            foreach (var folding in f)
            {
                if (folding.StartLine + 1 > document.LineCount ||
                    folding.EndLine + 1 > document.LineCount) continue;
                var sLine = document.GetLineByNumber(folding.StartLine + 1);
                var eLine = document.GetLineByNumber(folding.EndLine + 1);

                var sOff = sLine.Offset +
                           Math.Clamp(folding.StartCharacter ?? sLine.Length, 0, sLine.Length);
                var eOff = eLine.Offset +
                           Math.Clamp(folding.EndCharacter ?? eLine.Length, 0, eLine.Length);

                if (eOff <= sOff) continue;

                l.Add(new NewFolding(sOff, eOff));
            }

        // AvaloniaEdit requires foldings sorted by start offset, the language server does not guarantee any order
        l.Sort((a, b) =>
        {
            var startComparison = a.StartOffset.CompareTo(b.StartOffset);
            return startComparison != 0 ? startComparison : b.EndOffset.CompareTo(a.EndOffset);
        });

        return l;
    }
}

internal class FoldingEntry
{
    public FoldingEntry(string openChar, string closeChar,
        StringComparison comparisonMode = StringComparison.Ordinal)
    {
        OpenString = openChar;
        CloseString = closeChar;
        ComparisonMode = comparisonMode;
    }

    public string OpenString { get; set; }
    public string CloseString { get; set; }

    public StringComparison ComparisonMode { get; set; }
}
