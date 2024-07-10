using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Essentials.EditorExtensions
{
    public class FoldingStrategyBase : IFoldingStrategy
    {
        protected readonly List<FoldingEntry> Foldings = new();

        /// <summary>
        ///     Logic how code collapsing should work
        ///     Works but could be better ;)
        /// </summary>
        public FoldingStrategyBase()
        {
        }

        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            _ = UpdateFoldingsAsync(manager, document);
        }

        public async Task UpdateFoldingsAsync(FoldingManager manager, TextDocument document)
        {
            try
            {
                //var beforeFolding = DateTime.Now.TimeOfDay;
                
                IEnumerable<NewFolding> newFoldings = await CreateNewFoldingsAsync(document, out var firstErrorOffset);
                manager.UpdateFoldings(newFoldings, firstErrorOffset);

                //ContainerLocator.Container.Resolve<ILogger>()?.Log("Updated foldings after: " + (DateTime.Now.TimeOfDay - beforeFolding).Milliseconds + "ms", ConsoleColor.DarkGray);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            }
        }

        public Task<List<NewFolding>> CreateNewFoldingsAsync(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            var text = document.Text;

            return Task.Run(() =>
            {
                document = new TextDocument(text);
                var newFoldings = new List<NewFolding>();
                var startOffsets = new Stack<int>();
                var foldingValues = new Stack<FoldingEntry>();
                var lastNewLineOffset = 0;

                DocumentLine lastNewLine;
                var cstring = "";

                //GET MAX LENGTH FOR PERFORMANCE
                var maxlength = 0;
                foreach (var folding in Foldings)
                {
                    if (folding.OpenString.Length > maxlength) maxlength = folding.OpenString.Length;
                    if (folding.CloseString.Length > maxlength) maxlength = folding.CloseString.Length;
                }

                var lineComment = false;
                var blockComment = false;

                for (var i = 0; i < document.TextLength; i++)
                {
                    if (cstring.Length > maxlength) cstring = cstring.Remove(0, 1);
                    var c = document.GetCharAt(i);
                    cstring += c;

                    foreach (var folding in Foldings)
                        if (cstring.Length >= folding.OpenString.Length && cstring
                            .Substring(cstring.Length - folding.OpenString.Length, folding.OpenString.Length)
                            .Equals(folding.OpenString, folding.ComparisonMode))
                        {
                            if (!lineComment && !blockComment)
                            {
                                foldingValues.Push(folding);
                                startOffsets.Push(i);
                            }
                        }
                        else if (cstring.Length >= folding.CloseString.Length &&
                                 cstring.Substring(cstring.Length - folding.CloseString.Length,
                                     folding.CloseString.Length).Equals(folding.CloseString, folding.ComparisonMode) &&
                                 startOffsets.Count > 0)
                        {
                            if (blockComment && folding.CloseString != "*/") continue;
                            if(lineComment) continue;
                            var startOffset = startOffsets.Pop();
                            var startFolding = foldingValues.Pop();

                            var docLine = document.GetLineByOffset(startOffset);
                            var line = document.Text.Substring(docLine.Offset, docLine.Length);
                            var prevLine = docLine.PreviousLine;
                            var prevLineCheck = true;
                            if (prevLine != null)
                            {
                                var line2 = document.Text.Substring(prevLine.Offset, prevLine.Length);
                                var foldindex2 = line2.IndexOf(")");
                                if (foldindex2 >= 0) line2 = line2.Remove(foldindex2, 1);
                                if (string.IsNullOrWhiteSpace(line2)) prevLineCheck = false;
                            }

                            var sLine = line.Trim().Length > 0 ? line.Trim()[..^1] : "";
                            var lineCheck = string.IsNullOrWhiteSpace(sLine);

                            // don't fold if opening and closing brace are on the same line
                            if (startOffset < lastNewLineOffset && startFolding == folding)
                            {
                                if (docLine.Offset > 0 && prevLineCheck && lineCheck && prevLine != null)
                                    newFoldings.Add(new NewFolding(prevLine.EndOffset, i + 1));
                                else if (i + 1 > startOffset + 1) //END > START
                                    newFoldings.Add(new NewFolding(startOffset + 1, i + 1));
                            }
                        }
                        else if (c == '\n' || c == '\r')
                        {
                            lastNewLineOffset = i + 1;
                            lastNewLine = document.GetLineByOffset(i);
                        }


                    //Detect comment
                    if (cstring.Length > 1)
                    {
                        if (cstring[^2..] == "/*") blockComment = true;
                        if (cstring[^2..] == "--") lineComment = true;
                        if (c == '\n') lineComment = false;
                        if (cstring[^2..] == "*/") blockComment = false;
                    }
                }

                newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
                return newFoldings;
            });
        }
    }

    public class FoldingEntry
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