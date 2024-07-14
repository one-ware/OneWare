using OneWare.WaveFormViewer.Models;
using Xunit;

namespace OneWare.WaveFormViewer.Tests;

public class SignalConverterTests
{
    [Fact]
    public void ConvertBitsToDecimalTests()
    {
        Assert.Equal(-1000, SignalConverter.ConvertToSignedInt64("11111111111111111111110000011000", 32));
        Assert.Equal(16191, SignalConverter.ConvertToSignedInt64("11111100111111", 32));
        Assert.Equal(268434456, SignalConverter.ConvertToSignedInt64("00001111111111111111110000011000", 32));
    }

    [Fact]
    public void ConvertBitsHexTests()
    {
        Assert.Equal("X5", SignalConverter.ConvertToHexString("XZ0101"));
        Assert.Equal("ZA", SignalConverter.ConvertToHexString("ZZ1010"));
        Assert.Equal("XAXX", SignalConverter.ConvertToHexString("XZ101010XZX0XZXZ"));
        Assert.Equal("XA", SignalConverter.ConvertToHexString("ZXZX1010"));
        Assert.Equal("-5", SignalConverter.ConvertToHexString("--0101"));
    }

    [Fact]
    public void ConvertBitsAsciiTests()
    {
        Assert.Equal("Test", SignalConverter.ConvertToAsciiString("01010100011001010111001101110100"));
        Assert.Equal("XTest", SignalConverter.ConvertToAsciiString("XX01010100011001010111001101110100"));
    }

    [Fact]
    public void FixedPointShiftTests()
    {
        Assert.Equal(5, SignalConverter.PerformFixedPointShift(10, 1));
        Assert.Equal(-20, SignalConverter.PerformFixedPointShift(-10, -1));
        Assert.Equal(8, SignalConverter.PerformFixedPointShift(2, -2));
        Assert.Equal(4095.9375, SignalConverter.PerformFixedPointShift(65535, 4));

        const ulong test = 65535;
        Assert.Equal(4095.9375, SignalConverter.PerformFixedPointShift(test, 4));
    }

    [Fact]
    public void AutomaticFixedPointShiftTests()
    {
        Assert.Equal(5, WaveModel.GetAutomaticFixedPointShift("test[0:5]"));
        Assert.Equal(16, WaveModel.GetAutomaticFixedPointShift("x_out[15:-16]"));
        Assert.Equal(-15, WaveModel.GetAutomaticFixedPointShift("z_out[32:15]"));
        Assert.Equal(6, WaveModel.GetAutomaticFixedPointShift("y_out[4:10]"));
    }
}