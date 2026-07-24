using System.Text;
using OneWare.Terminal.Provider;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class UserInputEchoFilterTests
{
    private static string Filter(UserInputEchoFilter filter, string input)
    {
        return Encoding.UTF8.GetString(filter.Filter(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void PassesThroughWithoutUserInput()
    {
        var filter = new UserInputEchoFilter();
        Assert.Equal("program output\n", Filter(filter, "program output\n"));
    }

    [Fact]
    public void RemovesEchoedUserInput()
    {
        var filter = new UserInputEchoFilter();
        filter.OnUserInput(Encoding.UTF8.GetBytes("yes"));
        Assert.Equal("", Filter(filter, "yes"));
        Assert.Equal("more output", Filter(filter, "more output"));
    }

    [Fact]
    public void RemovesEchoAcrossChunks()
    {
        var filter = new UserInputEchoFilter();
        filter.OnUserInput(Encoding.UTF8.GetBytes("hello"));
        Assert.Equal("", Filter(filter, "he"));
        Assert.Equal("!", Filter(filter, "llo!"));
    }

    [Fact]
    public void EnterEchoedAsCrLfIsRemoved()
    {
        var filter = new UserInputEchoFilter();
        filter.OnUserInput(Encoding.UTF8.GetBytes("y\r"));
        Assert.Equal("output", Filter(filter, "y\r\noutput"));
    }

    [Fact]
    public void MismatchStopsFiltering()
    {
        var filter = new UserInputEchoFilter();
        filter.OnUserInput(Encoding.UTF8.GetBytes("abc"));
        // Program output that does not match the pending input must survive intact.
        Assert.Equal("xyz", Filter(filter, "xyz"));
        Assert.Equal("abc", Filter(filter, "abc"));
    }

    [Fact]
    public void PartialMatchThenMismatchPassesRemainderThrough()
    {
        var filter = new UserInputEchoFilter();
        filter.OnUserInput(Encoding.UTF8.GetBytes("ab"));
        Assert.Equal("Z", Filter(filter, "aZ"));
    }
}
