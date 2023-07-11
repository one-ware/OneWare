using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace OneWare.Shared.EditorExtensions;

public class RegexFoldingStrategy : IFoldingStrategy
{
	private const string FoldingStartPattern = @"(?x)
		# From the start of the line make sure we are not going into a comment ...
		^(
			([^-]-?(?!-))*?
				(
				# Check for keyword ... is
				 (\b(?i:architecture|case|entity|function|package|procedure)\b(.+?)(?i:\bis)\b)

				# Check for if statements
				|(\b(?i:if)\b(.+?)(?i:generate|then)\b)

				# Check for and while statements
				|(\b(?i:for|while)(.+?)(?i:loop|generate)\b)

				# Check for keywords that do not require an is after it
				|(\b(?i:component|process|record)\b[^;]*?$)

				# From the beginning of the line, check for instantiation maps
				|(^\s*\b(?i:port|generic)\b(?i:\s+map\b)?\s*\()
			)
		)
	";

	private const string FoldingEndPattern = @"(?x)
		# From the start of the line ...
		^(
			(
				(
					# Make sure we are not going into a comment ...
					([^-]-?(?!-))*?
						(
							# The word end to the end of the line
			 				(?i:\bend\b).*$\n?
						)
					)
				)

				# ... a close paren followed by an optional semicolon as the only thing on the line
			    |(\s*?\)\s*?;?\s*?$\n?
			)
		)
	";

	private static readonly Regex FoldingStart = new(FoldingStartPattern);
	private static readonly Regex FoldingEnd = new(FoldingEndPattern);

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var foldings = CreateNewFoldings(document, out var firstErrorOffset);
        manager.UpdateFoldings(foldings, firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        var start = FoldingStart.Match(document.Text);
        var end = FoldingEnd.Match(document.Text, start.Index);
        
        Console.WriteLine(start.Success);
        Console.WriteLine(end.Success);

        firstErrorOffset = -1;
        
        return new NewFolding[] { };
    }
}