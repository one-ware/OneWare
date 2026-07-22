using System.Text;
using OneWare.Terminal.Provider;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class PseudoTerminalConnectionTests
{
    private const string MarkerCommand =
        "__ow_exit=$?; printf '\\033[1A\\r\\033[2K\\033]9;OW_DONE:0123456789abcdef:%s\\007' \"$__ow_exit\"";

    [Fact]
    public void FilterOutput_SuppressesSequenceAcrossChunks()
    {
        var connection = new PseudoTerminalConnection(null!);
        var marker = Encoding.ASCII.GetBytes(MarkerCommand);
        connection.SuppressOutput(marker);

        var first = connection.FilterOutput(Encoding.ASCII.GetBytes($"before{MarkerCommand[..30]}"));
        var second = connection.FilterOutput(Encoding.ASCII.GetBytes($"{MarkerCommand[30..]}after"));

        Assert.Equal("before", Encoding.ASCII.GetString(first));
        Assert.Equal("after", Encoding.ASCII.GetString(second));
    }

    [Fact]
    public void FilterOutput_SuppressesWrappedReadlineEcho()
    {
        var connection = new PseudoTerminalConnection(null!);
        connection.SuppressOutput(Encoding.ASCII.GetBytes(MarkerCommand));

        var wrappedEcho = MarkerCommand
            .Replace("\\033[1A", "\\\\\r\\033[1A")
            .Replace("$__ow_exit\"", "$__ow_ex\rxit\"");
        var first = connection.FilterOutput(Encoding.ASCII.GetBytes($"prompt{wrappedEcho[..48]}"));
        var second = connection.FilterOutput(Encoding.ASCII.GetBytes(wrappedEcho[48..]));

        Assert.Equal("prompt", Encoding.ASCII.GetString(first));
        Assert.Empty(second);
    }

    [Fact]
    public void FilterOutput_ReleasesIncompleteSuppressionAtLineEnd()
    {
        var connection = new PseudoTerminalConnection(null!);
        connection.SuppressOutput(Encoding.ASCII.GetBytes(MarkerCommand));

        var output = connection.FilterOutput(Encoding.ASCII.GetBytes("__ow_broken\r\nnext"));

        Assert.Equal("__ow_broken\r\nnext", Encoding.ASCII.GetString(output));
    }
}
