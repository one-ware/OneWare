using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace OneWare.Shared.EditorExtensions;

public class RegexFoldingStrategy : IFoldingStrategy
{
	private readonly Regex FoldingStart;
	private readonly Regex FoldingEnd;

	public RegexFoldingStrategy(Regex foldingStart, Regex foldingEnd)
	{
		FoldingStart = foldingStart;
		FoldingEnd = foldingEnd;
	}

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var foldings = CreateNewFoldings(document, out var firstErrorOffset);
        manager.UpdateFoldings(foldings, firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
	    firstErrorOffset = -1;
	    var newFoldings = new List<NewFolding>();

	    var startOffsets = new Stack<int>();
	    foreach (var line in document.Lines)
        {
	        var lineText = document.GetText(line.Offset, line.Length);
	        var start = FoldingStart.Match(lineText);
	        var end = FoldingEnd.Match(lineText);

	        if (start.Success && !end.Success)
	        {
		        startOffsets.Push(line.Offset + start.Index + start.Length);
	        }

	        if (end.Success && !start.Success && startOffsets.Any())
	        {
		        newFoldings.Add(new NewFolding(startOffsets.Pop(), line.Offset + end.Index));
	        }
        }

        return newFoldings.OrderBy(x => x.StartOffset);
    }
}