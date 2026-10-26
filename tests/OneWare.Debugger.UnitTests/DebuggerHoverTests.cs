using OneWare.Debugger.Helpers;
using Xunit;

namespace OneWare.Debugger.UnitTests;

public class DebuggerHoverTests
{
    [Fact]
    public void ScalarStaysOneNode()
    {
        var node = GdbValueParser.Parse("x", "42");

        Assert.Equal("x", node.Name);
        Assert.Equal("42", node.Value);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void StructureBecomesNamedChildren()
    {
        var node = GdbValueParser.Parse("p", "{x = 1, pos = {a = 2, b = 0x4011a6 <main+4>}, s = \"a, }b\"}");

        Assert.Equal(["x", "pos", "s"], node.Children.Select(x => x.Name));
        Assert.Equal("1", node.Children[0].Value);
        Assert.Equal(["a", "b"], node.Children[1].Children.Select(x => x.Name));
        Assert.Equal("0x4011a6 <main+4>", node.Children[1].Children[1].Value);
        Assert.Equal("\"a, }b\"", node.Children[2].Value);
    }

    [Fact]
    public void ArrayElementsAreNumbered()
    {
        var node = GdbValueParser.Parse("arr", "{1, 2, 3}");

        Assert.Equal(["[0]", "[1]", "[2]"], node.Children.Select(x => x.Name));
        Assert.Equal("3", node.Children[2].Value);
    }

    [Fact]
    public void RepeatedCharsStayWithTheirMember()
    {
        var node = GdbValueParser.Parse("s", "{buf = \"hi\", '\\000' <repeats 18 times>, n = 3}");

        Assert.Equal(["buf", "n"], node.Children.Select(x => x.Name));
        Assert.Equal("\"hi\", '\\000' <repeats 18 times>", node.Children[0].Value);
    }

    [Theory]
    [InlineData("{int (int)} 0x401126 <f>")]
    [InlineData("{a = 1")]
    public void UnreadableValueStaysScalar(string value)
    {
        var node = GdbValueParser.Parse("v", value);

        Assert.Equal(value, node.Value);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void DepthLimitIsALeaf()
    {
        var node = GdbValueParser.Parse("t", "{inner = {...}}");

        Assert.Equal("{...}", node.Children[0].Value);
        Assert.Empty(node.Children[0].Children);
    }

    [Theory]
    [InlineData("  return p->pos.y;", 17, "p->pos.y")]
    [InlineData("  return p->pos.y;", 10, "p")]
    [InlineData("  return p->pos.y;", 13, "p->pos")]
    [InlineData("  int count = 0;", 8, "count")]
    [InlineData("  return p->pos.y;", 4, null)]
    [InlineData("  x = 123;", 7, null)]
    [InlineData("  x = a + b;", 5, null)]
    public void ExtractsMemberAccessChain(string line, int column, string? expected)
    {
        Assert.Equal(expected, HoverExpression.Extract(line, column));
    }

    [Fact]
    public void RootIsTheFirstIdentifier()
    {
        Assert.Equal("p", HoverExpression.RootOf("p->pos.y"));
    }
}
