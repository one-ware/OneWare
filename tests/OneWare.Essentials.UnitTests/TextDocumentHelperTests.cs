using AvaloniaEdit.Document;
using OneWare.Essentials.Helpers;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class TextDocumentHelperTests
{
    [Fact]
    public void GetStartAndEndOffset_WhenStartLineIsLessThanZero_ReturnsOne()
    {
        var document = new TextDocument();
        document.Text = "Test\nTest\nTest";
        
        var result = document.GetStartAndEndOffset(-1, -1, 1000, 1000);
        
        Assert.Equal(0, result.startOffset);
        Assert.Equal(0, result.endOffset);
        
        result = document.GetStartAndEndOffset(1, 1, 1000, 1000);
        
        Assert.Equal(0, result.startOffset);
        Assert.Equal(document.TextLength, result.endOffset);
    }
}