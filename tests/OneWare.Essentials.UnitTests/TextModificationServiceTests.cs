using AvaloniaEdit.Document;
using OneWare.Essentials.EditorExtensions;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class TextModificationServiceTests
{
    [Fact]
    public void TryClampToLine_MultiLineSegment_StaysWithinEachLineAndDoesNotMutateSegment()
    {
        var document = new TextDocument { Text = "first\nsecond line\nthird" };
        var segment = new TextModificationSegment(2, document.TextLength - 2);
        var originalStart = segment.StartOffset;
        var originalEnd = segment.EndOffset;

        foreach (var line in document.Lines)
        {
            Assert.True(TextModificationService.TryClampToLine(segment, line, out var start, out var end));
            Assert.InRange(start, line.Offset, line.EndOffset);
            Assert.InRange(end, line.Offset, line.EndOffset);
            Assert.True(end > start);
        }

        Assert.Equal(originalStart, segment.StartOffset);
        Assert.Equal(originalEnd, segment.EndOffset);
    }

    [Fact]
    public void TryClampToLine_SegmentOnlyTouchingLineDelimiter_ReturnsFalse()
    {
        var document = new TextDocument { Text = "ab\ncd" };
        var firstLine = document.GetLineByNumber(1);
        var segment = new TextModificationSegment(firstLine.EndOffset, firstLine.EndOffset + 1);

        Assert.False(TextModificationService.TryClampToLine(segment, firstLine, out _, out _));
    }
}
