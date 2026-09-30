using System.Linq;
using OneWare.SearchList.Helpers;
using Xunit;

namespace OneWare.SearchList.UnitTests;

public class SearchReplaceHelperTests
{
    [Theory]
    [InlineData(@"Ad\1e", "Ad${1}e")]
    [InlineData("Ad$1e", "Ad$1e")]
    [InlineData(@"\1\2", "${1}${2}")]
    [InlineData(@"\10", "${1}0")]
    [InlineData(@"a\nb\tc", "a\nb\tc")]
    [InlineData(@"C:\\temp", @"C:\temp")]
    [InlineData(@"\\1", @"\1")]
    [InlineData(@"a\x", @"a\x")]
    [InlineData(@"end\", @"end\")]
    [InlineData("plain", "plain")]
    public void ToRegexReplacement_ConvertsBackslashSyntax(string input, string expected)
    {
        Assert.Equal(expected, SearchReplaceHelper.ToRegexReplacement(input));
    }

    [Fact]
    public void ReplaceAll_Regex_ExpandsCaptureGroups_IssueExample()
    {
        var regex = SearchReplaceHelper.TryBuildRegex("A.(..)e", true, true, false)!;

        var result = SearchReplaceHelper.ReplaceAll("Abcde\nAfgge\n", regex, @"Ad\1e", true);

        Assert.Equal("Adcde\nAdgge\n", result);
    }

    [Fact]
    public void ReplaceAll_Regex_SupportsDotNetAndNamedGroups()
    {
        var regex = SearchReplaceHelper.TryBuildRegex(@"(?<name>\w+)_s", true, true, false)!;

        var result = SearchReplaceHelper.ReplaceAll("clk_s rst_s", regex, "${name}_sig $$", true);

        Assert.Equal("clk_sig $ rst_sig $", result);
    }

    [Fact]
    public void ReplaceAll_Regex_RenamesSignalConversions()
    {
        var regex = SearchReplaceHelper.TryBuildRegex(@"to_integer\(unsigned\((\w+)\)\)", true, true, false)!;

        var result = SearchReplaceHelper.ReplaceAll("x <= to_integer(unsigned(a_s));", regex, "to_int($1)", true);

        Assert.Equal("x <= to_int(a_s);", result);
    }

    [Fact]
    public void ReplaceAll_Literal_InsertsDollarAndBackslashVerbatim()
    {
        var regex = SearchReplaceHelper.TryBuildRegex("a.b", true, false, false)!;

        var result = SearchReplaceHelper.ReplaceAll("a.b axb", regex, @"$1\1", false);

        Assert.Equal(@"$1\1 axb", result);
    }

    [Fact]
    public void ReplaceAt_Regex_ReplacesOnlyTheSelectedMatch()
    {
        var text = "Abcde\nAfgge\n";
        var regex = SearchReplaceHelper.TryBuildRegex("A.(..)e", true, true, false)!;

        var result = SearchReplaceHelper.ReplaceAt(text, regex, 6, 5, "Ad$1e", true);

        Assert.Equal("Abcde\nAdgge\n", result);
    }

    [Fact]
    public void ReplaceAt_ReturnsNull_WhenTextChangedSinceSearch()
    {
        var regex = SearchReplaceHelper.TryBuildRegex("A.(..)e", true, true, false)!;

        Assert.Null(SearchReplaceHelper.ReplaceAt("xAbcde", regex, 0, 5, "y", true));
        Assert.Null(SearchReplaceHelper.ReplaceAt("Abcde", regex, 0, 4, "y", true));
        Assert.Null(SearchReplaceHelper.ReplaceAt("Abcde", regex, 3, 5, "y", true));
    }

    [Fact]
    public void ReplaceAt_KeepsWordBoundaryContext()
    {
        var regex = SearchReplaceHelper.TryBuildRegex("foo", true, false, true)!;

        Assert.Null(SearchReplaceHelper.ReplaceAt("xfoo foo", regex, 1, 3, "bar", false));
        Assert.Equal("xfoo bar", SearchReplaceHelper.ReplaceAt("xfoo foo", regex, 5, 3, "bar", false));
    }

    [Fact]
    public void TryBuildRegex_ReturnsNull_ForIncompletePattern()
    {
        Assert.Null(SearchReplaceHelper.TryBuildRegex("(abc", true, true, false));
        Assert.NotNull(SearchReplaceHelper.TryBuildRegex("(abc", true, false, false));
    }

    [Fact]
    public void FindMatches_ReportsRegexMatchLengthAndLine()
    {
        var text = "first\r\n  Abcde tail\r\nAfgge";
        var regex = SearchReplaceHelper.TryBuildRegex("A.(..)e", true, true, false)!;

        var matches = SearchReplaceHelper.FindMatches(text, regex).ToList();

        Assert.Equal(2, matches.Count);
        Assert.Equal(new SearchMatch(9, 5, 2, "  Abcde tail", "", "Abcde", " tail"), matches[0]);
        Assert.Equal(new SearchMatch(21, 5, 3, "Afgge", "", "Afgge", ""), matches[1]);
        Assert.Equal("Abcde", text.Substring(matches[0].StartOffset, matches[0].Length));
    }

    [Fact]
    public void FindMatches_SkipsEmptyMatches()
    {
        var regex = SearchReplaceHelper.TryBuildRegex("x*", true, true, false)!;

        var matches = SearchReplaceHelper.FindMatches("axxb", regex).ToList();

        Assert.Single(matches);
        Assert.Equal(1, matches[0].StartOffset);
        Assert.Equal(2, matches[0].Length);
    }
}
