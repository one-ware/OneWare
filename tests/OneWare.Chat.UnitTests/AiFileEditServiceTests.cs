using OneWare.Chat.Services;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class AiFileEditServiceTests
{
    private const string Original = "A\nB\nC\nD\nE";

    [Theory]
    // A trailing newline in the content is a terminator, not a separator.
    [InlineData(3, 1, "X\n", "A\nB\nX\nD\nE")]
    [InlineData(3, 1, "X", "A\nB\nX\nD\nE")]
    [InlineData(3, 0, "X\n", "A\nB\nX\nC\nD\nE")]
    [InlineData(3, 2, "X\nY\n", "A\nB\nX\nY\nE")]
    [InlineData(3, 1, "X\nY", "A\nB\nX\nY\nD\nE")]
    [InlineData(1, 1, "X\n", "X\nB\nC\nD\nE")]
    [InlineData(3, 1, "X\r\n", "A\nB\nX\nD\nE")]
    // Only a single trailing newline is dropped; extra ones are intentional blank lines.
    [InlineData(3, 1, "X\n\n", "A\nB\nX\n\nD\nE")]
    [InlineData(3, 1, "\n", "A\nB\n\nD\nE")]
    // Deletion without replacement content.
    [InlineData(3, 1, "", "A\nB\nD\nE")]
    public void ApplyLineEdit_TreatsTrailingNewlineAsTerminator(int startLine, int lineCount, string content,
        string expected)
    {
        Assert.Equal(expected, AiFileEditService.ApplyLineEdit(Original, startLine, lineCount, content));
    }

    [Fact]
    public void ApplyLineEdit_IsIdempotentWhenRepeated()
    {
        var once = AiFileEditService.ApplyLineEdit(Original, 3, 1, "X\n");
        var twice = AiFileEditService.ApplyLineEdit(once, 3, 1, "X\n");

        Assert.Equal(once, twice);
    }

    [Fact]
    public void ApplyLineEdit_PreservesTrailingNewlineOfOriginal()
    {
        Assert.Equal("A\nB\nX\n", AiFileEditService.ApplyLineEdit("A\nB\nC\n", 3, 1, "X\n"));
    }

    [Fact]
    public void ApplyLineEdit_PreservesWindowsNewlinesOfOriginal()
    {
        Assert.Equal("A\r\nB\r\nX\r\nD", AiFileEditService.ApplyLineEdit("A\r\nB\r\nC\r\nD", 3, 1, "X\n"));
    }
}
