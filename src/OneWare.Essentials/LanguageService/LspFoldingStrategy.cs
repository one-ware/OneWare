using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.LanguageService
{
    public class LspFoldingStrategy : IFoldingStrategy
    {
        private readonly List<FoldingEntry> _foldings = new();
        private readonly LanguageServiceLsp _languageService;

        private readonly IFile _projectfile;

        /// <summary>
        ///     Logic how code collapsing should work
        ///     Works but could be better ;)
        /// </summary>
        public LspFoldingStrategy(LanguageServiceLsp ls, IFile file)
        {
            _languageService = ls;
            _projectfile = file;
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
            catch (Exception)
            {
            }
        }

        public async Task<IEnumerable<NewFolding>> CreateNewFoldingsAsync(TextDocument document)
        {
            var l = new List<NewFolding>();
            if (!_languageService.IsLanguageServiceReady) return l;
            var f = await _languageService.RequestFoldingsAsync(_projectfile.FullPath);
            if (f is not null)
                foreach (var folding in f)
                {
                    if (folding.StartLine + 1 >= document.LineCount ||
                        folding.EndLine + 1 >= document.LineCount) continue;
                    var sLine = document.GetLineByNumber(folding.StartLine + 1);
                    var eLine = document.GetLineByNumber(folding.EndLine + 1);
                    //var sChar = folding.StartCharacter + 1 > sLine.L
                    var sOff = sLine.Offset +
                               (folding.StartCharacter.HasValue ? folding.StartCharacter.Value : sLine.Length);
                    var eOff = eLine.Offset +
                               (folding.EndCharacter.HasValue ? folding.EndCharacter.Value : eLine.Length);
                    l.Add(new NewFolding(sOff, eOff));
                }

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
}