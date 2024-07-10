using System.Text.RegularExpressions;

namespace OneWare.Vhdl.Folding;

public static class FoldingRegexVhdl
{
    private const string FoldingStartPattern = """
                                               (?x)
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
                                               	
                                               """;

    private const string FoldingEndPattern = """
                                             (?x)
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
                                             	
                                             """;

    public static readonly Regex FoldingStart = new(FoldingStartPattern, RegexOptions.Multiline);

    public static readonly Regex FoldingEnd = new(FoldingEndPattern, RegexOptions.Multiline);
}