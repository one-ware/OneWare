using System.Linq;
using OneWare.TerminalManager.Models;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class HeadTailOutputBufferTests
{
    [Fact]
    public void ToString_ReturnsShortOutputUnchanged()
    {
        var buffer = new HeadTailOutputBuffer(100);
        buffer.Append("hello ");
        buffer.Append("world");

        Assert.Equal("hello world", buffer.ToString());
        Assert.Equal(11, buffer.TotalLength);
        Assert.Equal(0, buffer.OmittedLength);
    }

    [Fact]
    public void ToString_KeepsBeginningAndEndOfLongOutput()
    {
        var buffer = new HeadTailOutputBuffer(20);
        var text = string.Concat(Enumerable.Range(0, 1000).Select(i => (char)('a' + i % 26)));

        // Chunks of different sizes, like pty reads.
        for (var i = 0; i < text.Length;)
        {
            var size = System.Math.Min(1 + i % 37, text.Length - i);
            buffer.Append(text.Substring(i, size));
            i += size;
        }

        Assert.Equal(1000, buffer.TotalLength);
        Assert.Equal(980, buffer.OmittedLength);
        Assert.Equal($"{text[..10]}\n[... 0 lines (980 characters) of output omitted ...]\n{text[^10..]}",
            buffer.ToString());
    }

    [Fact]
    public void Counts_TreatTerminalLineBreaksAsOneCharacter()
    {
        var buffer = new HeadTailOutputBuffer(1000);
        // "\r\n" split across two reads, as the pty may deliver it.
        buffer.Append("out\r");
        buffer.Append("\nerr\r\n");

        Assert.Equal(8, buffer.TotalLength);
        Assert.Equal(2, buffer.LineCount);

        buffer.Append("no newline");
        Assert.Equal(3, buffer.LineCount);
    }

    [Fact]
    public void ToString_ReportsOmittedLinesOfLongOutput()
    {
        var buffer = new HeadTailOutputBuffer(40);
        for (var i = 1; i <= 1000; i++)
            buffer.Append($"{i:0000}\r\n");

        Assert.Equal(1000, buffer.LineCount);
        Assert.Equal(5000, buffer.TotalLength);

        var lines = buffer.ToString().Split('\n');
        Assert.StartsWith("0001", lines[0]);
        Assert.StartsWith("1000", lines[^2]);
        var completeLines = lines.Count(l => l.Length == 5 && char.IsDigit(l[0]));
        var marker = lines.Single(l => l.StartsWith("[..."));
        // Head and tail hold 20 characters each: lines 1-3 plus "00", and the end of line 997 plus lines 998-1000.
        Assert.Equal("[... 993 lines (4967 characters) of output omitted ...]", marker);
        Assert.Equal(6, completeLines);
    }

    [Fact]
    public void Clear_StartsOver()
    {
        var buffer = new HeadTailOutputBuffer(10);
        buffer.Append(new string('x', 100));
        buffer.Clear();
        buffer.Append("new");

        Assert.Equal("new", buffer.ToString());
        Assert.Equal(3, buffer.TotalLength);
    }
}
