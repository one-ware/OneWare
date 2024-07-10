using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace OneWare.Essentials.EditorExtensions;

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

	    var startOffsets = new Stack<(int offset, string name)>();
	    foreach (var line in document.Lines)
        {
	        var lineText = document.GetText(line.Offset, line.Length);
	        var start = FoldingStart.Match(lineText);
	        var end = FoldingEnd.Match(lineText);

	        if (start.Success && !end.Success)
	        {
		        startOffsets.Push((line.Offset + start.Index, start.Value.TrimEnd()));
	        }

	        if (end.Success && !start.Success && startOffsets.Any())
	        {
		        var startPop = startOffsets.Pop();
		        newFoldings.Add(new NewFolding(startPop.offset, line.Offset + end.Index + end.Length)
		        {
			        Name = startPop.name + "..." + end.Value.TrimStart()
		        });
	        }
        }

        return newFoldings.OrderBy(x => x.StartOffset);
    }
}