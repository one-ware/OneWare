using AvaloniaEdit;
using AvaloniaEdit.Document;
using Xunit;

namespace OneWare.Python.UnitTests;

public class PythonIndentationTests
{
    [Theory]
    [InlineData("value = 1", "")]
    [InlineData("    value = 1", "    ")]
    [InlineData("\tvalue = 1", "\t")]
    [InlineData("    ", "    ")]
    [InlineData("if ready:", "    ")]
    [InlineData("    for item in items:  # next item", "        ")]
    [InlineData("async def run():", "    ")]
    [InlineData("class Example:", "    ")]
    [InlineData("match value:", "    ")]
    [InlineData("    case 1:", "        ")]
    [InlineData("if value == '#:':", "    ")]
    [InlineData("if value == '\\'':", "    ")]
    [InlineData("    # if ready:", "    ")]
    [InlineData("    value = 1 # comment:", "    ")]
    [InlineData("    value = 'text:'", "    ")]
    [InlineData("    value = \"unfinished:", "    ")]
    [InlineData("    value = 'escaped\\':", "    ")]
    [InlineData("    values = {'key':", "    ")]
    [InlineData("values = {\n    'key':", "    ")]
    [InlineData("values = items[\n    start:", "    ")]
    [InlineData("text = '''\n    if ready:", "    ")]
    [InlineData("text = \"\"\"\n    if ready:", "    ")]
    [InlineData("text = '''\nif ready:\n'''\nif ready:", "    ")]
    [InlineData("if (\n    ready\n):", "    ")]
    [InlineData("if ready: run()", "")]
    public void NewlinePreservesIndentationAndIndentsBlockHeaders(string precedingText, string expectedIndentation)
    {
        var document = new TextDocument(precedingText + "\n");
        var strategy = CreateStrategy();

        strategy.IndentLine(document, document.Lines[^1]);

        Assert.Equal(precedingText + "\n" + expectedIndentation, document.Text);
    }

    [Fact]
    public void NewlineReplacesLeadingWhitespaceWithoutChangingFollowingText()
    {
        var document = new TextDocument("    if ready:\n  run()");

        CreateStrategy().IndentLine(document, document.Lines[1]);

        Assert.Equal("    if ready:\n        run()", document.Text);
    }

    [Fact]
    public void NewlineUsesCurrentEditorOptions()
    {
        var options = new TextEditorOptions { ConvertTabsToSpaces = true, IndentationSize = 2 };
        var strategy = new PythonIndentationStrategy(options);
        var document = new TextDocument("if ready:\n");

        strategy.IndentLine(document, document.Lines[1]);
        Assert.Equal("if ready:\n  ", document.Text);

        options.ConvertTabsToSpaces = false;
        document.Text = "if ready:\n";
        strategy.IndentLine(document, document.Lines[1]);
        Assert.Equal("if ready:\n\t", document.Text);
    }

    [Fact]
    public void FirstLineIsUnchanged()
    {
        var document = new TextDocument("    value = 1");

        CreateStrategy().IndentLine(document, document.Lines[0]);

        Assert.Equal("    value = 1", document.Text);
    }

    [Fact]
    public void IndentLinesDoesNotChangeExistingBlockStructure()
    {
        const string text = "if ready:\n    run()\nfinish()\n";
        var document = new TextDocument(text);
        var strategy = CreateStrategy();

        strategy.IndentLines(document, 1, document.LineCount);
        strategy.IndentLines(document, 2, 3);

        Assert.Equal(text, document.Text);
    }

    [Fact]
    public void NewlineSupportsCrLf()
    {
        var document = new TextDocument("if ready:\r\n");

        CreateStrategy().IndentLine(document, document.Lines[1]);

        Assert.Equal("if ready:\r\n    ", document.Text);
    }

    private static PythonIndentationStrategy CreateStrategy() =>
        new(new TextEditorOptions { ConvertTabsToSpaces = true, IndentationSize = 4 });
}
