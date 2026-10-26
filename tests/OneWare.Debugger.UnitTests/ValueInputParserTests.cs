using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using Xunit;

namespace OneWare.Debugger.UnitTests;

public class ValueInputParserTests
{
    [Theory]
    [InlineData("12")]
    [InlineData("0x12")]
    [InlineData("0X12")]
    [InlineData(" 0x0012 ")]
    public void HexAcceptsInputWithAndWithoutPrefix(string input)
    {
        Assert.True(ValueInputParser.TryParse(input, NumberBase.Hex, 16, out var value, out _));
        Assert.Equal(0x12UL, value);
    }

    [Theory]
    [InlineData("18", 0x12UL)]
    [InlineData("+7", 0x7UL)]
    [InlineData("-1", 0xFFFFUL)]
    [InlineData("65535", 0xFFFFUL)]
    [InlineData("-32768", 0x8000UL)]
    [InlineData("-0", 0x0UL)]
    public void DecAcceptsSignedAndUnsignedValuesWithinTheWidth(string input, ulong expected)
    {
        Assert.True(ValueInputParser.TryParse(input, NumberBase.Dec, 16, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("65536")]
    [InlineData("-32769")]
    public void DecRejectsValuesOutsideTheWidth(string input)
    {
        Assert.False(ValueInputParser.TryParse(input, NumberBase.Dec, 16, out _, out var error));
        Assert.Equal("The value does not fit in 16 bits.", error);
    }

    [Theory]
    [InlineData("10010")]
    [InlineData("0b10010")]
    [InlineData("0000 0000 0001 0010")]
    public void BinAcceptsPrefixAndNibbleGroups(string input)
    {
        Assert.True(ValueInputParser.TryParse(input, NumberBase.Bin, 16, out var value, out _));
        Assert.Equal(0x12UL, value);
    }

    [Theory]
    [InlineData("22")]
    [InlineData("0o22")]
    public void OctAcceptsInputWithAndWithoutPrefix(string input)
    {
        Assert.True(ValueInputParser.TryParse(input, NumberBase.Oct, 16, out var value, out _));
        Assert.Equal(0x12UL, value);
    }

    [Theory]
    [InlineData("19", NumberBase.Oct, "\"19\" is not a valid octal number.")]
    [InlineData("12", NumberBase.Bin, "\"12\" is not a valid binary number.")]
    [InlineData("0x", NumberBase.Hex, "\"0x\" is not a valid hexadecimal number.")]
    [InlineData("-", NumberBase.Dec, "\"-\" is not a valid decimal number.")]
    [InlineData("-1", NumberBase.Hex, "\"-1\" is not a valid hexadecimal number.")]
    public void RejectsDigitsOutsideTheBase(string input, NumberBase numberBase, string expectedError)
    {
        Assert.False(ValueInputParser.TryParse(input, numberBase, 16, out _, out var error));
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void EmptyInputAsksForAValue()
    {
        Assert.False(ValueInputParser.TryParse("  ", NumberBase.Hex, 16, out _, out var error));
        Assert.Equal("Enter a value.", error);
    }

    [Fact]
    public void SixtyFourBitValuesDoNotOverflow()
    {
        Assert.True(ValueInputParser.TryParse("0xFFFFFFFFFFFFFFFF", NumberBase.Hex, 64, out var max, out _));
        Assert.Equal(ulong.MaxValue, max);

        Assert.True(ValueInputParser.TryParse("-1", NumberBase.Dec, 64, out var minusOne, out _));
        Assert.Equal(ulong.MaxValue, minusOne);

        Assert.False(ValueInputParser.TryParse("0x1FFFFFFFFFFFFFFFF", NumberBase.Hex, 64, out _, out _));
    }
}
