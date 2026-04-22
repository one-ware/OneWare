using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneWare.Vcd.Parser.Data;
using Xunit;
using Xunit.Abstractions;

namespace OneWare.Vcd.Parser.UnitTests;

public class VcdParserTests
{
    private readonly ITestOutputHelper _output;

    public VcdParserTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SignalLineTest()
    {
        var changeTimes = new List<long> { 0, 1000, 10000, 100000 };
        var signal = new VcdSignal<StdLogic[]>(changeTimes, VcdLineType.Reg, 4, "#", "Line");

        var testValue = new[] { StdLogic.Full, StdLogic.X };
        var testValue2 = new[] { StdLogic.Zero, StdLogic.X };
        var testValue3 = new[] { StdLogic.Full, StdLogic.Z };
        var testValue4 = new[] { StdLogic.Zero, StdLogic.Z };

        signal.AddChange(0, testValue);
        signal.AddChange(1, testValue2);
        signal.AddChange(2, testValue3);
        signal.AddChange(3, testValue4);

        Assert.Equal(testValue, signal.GetValueFromOffset(0));
        Assert.Equal(testValue3, signal.GetValueFromOffset(10000));
        Assert.Equal(testValue3, signal.GetValueFromOffset(10001));
        Assert.Equal(1, signal.FindIndex(1005));
        Assert.Equal(3, signal.FindIndex(1000000000));
        Assert.Equal(testValue4, signal.GetValueFromIndex(3));
    }

    [Fact]
    public void SignalLineTest2()
    {
        var changeTimes = new List<long> { 0, 1000, 10000, 100000 };
        var signal = new VcdSignal<StdLogic[]>(changeTimes, VcdLineType.Reg, 4, "#", "Line");
        var testValue = new[] { StdLogic.Full, StdLogic.X };

        signal.AddChange(0, testValue);

        Assert.Equal(testValue, signal.GetValueFromOffset(0));
    }

    [Theory]
    [InlineData("GHDL.vcd", 5, 24004)]
    [InlineData("tb_scale_bias_correction.vcd", 37, 42)]
    [InlineData("tb_top_aurora.vcd", 216, 130)]
    public void ParseTest_WithExpectedDefinitionCounts(string assetName, int signalCount, int changeTimeCount)
    {
        var result = ParseVcdWithMetrics(assetName);

        Assert.Equal(signalCount, result.Definition.SignalRegister.Count);
        Assert.Equal(changeTimeCount, result.Definition.ChangeTimes.Count);
    }

    [Theory]
    [InlineData("core.vcd")]
    [InlineData("trace.vcd")]
    [InlineData("waveform.vcd")]
    public void ParseTest_Smoke(string assetName)
    {
        var result = ParseVcdWithMetrics(assetName);

        Assert.NotNull(result);
        Assert.NotNull(result.Definition);
    }

    [Fact]
    public void ParseTest_IntegerWidth()
    {
        var result = VcdParser.ParseVcd(GetAssetPath("integer_width.vcd"));
        var signal = result.GetSignal("!");

        Assert.NotNull(signal);
        Assert.Equal(32, signal.BitWidth);

        var expected = Enumerable.Repeat(StdLogic.Zero, 31)
            .Append(StdLogic.Full)
            .ToList();
        Assert.Equal(expected, signal.GetValueFromOffset(0));
    }

    [Fact]
    public void ParseTest_EscapedSignalName()
    {
        var result = VcdParser.ParseVcd(GetAssetPath("code_gen_tb.vcd"));
        var signal = result.GetSignal("\\,");

        Assert.NotNull(signal);
        Assert.Equal(1, signal.BitWidth);
    }

    [Fact]
    public async Task FindLastTimeTest()
    {
        var lastTime = await VcdParser.TryFindLastTime(GetAssetPath("GHDL.vcd"));

        Assert.Equal(1000079333000, lastTime);
    }

    [Fact]
    public async Task ParseTestMulticore()
    {
        await ParseMulticore("GHDL.vcd", 1);
        await ParseMulticore("GHDL.vcd", 2);
        await ParseMulticore("GHDL.vcd", 4);
        await ParseMulticore("GHDL.vcd", 8);
    }

    private VcdFile ParseVcdWithMetrics(string assetName)
    {
        var sw = Stopwatch.StartNew();
        var before = GC.GetTotalMemory(true);
        var result = VcdParser.ParseVcd(GetAssetPath(assetName));
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"{assetName}: parsing took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"{assetName}: memory occupied: {(after - before) / 1000}kB");

        return result;
    }

    private async Task ParseMulticore(string assetName, int threads)
    {
        var path = GetAssetPath(assetName);
        var sw = Stopwatch.StartNew();
        var vcdFile = VcdParser.ParseVcdDefinition(path);

        var before = GC.GetTotalMemory(true);
        await VcdParser.ReadSignalsAsync(path, vcdFile, new Progress<(int, int)>(),
            CancellationToken.None, threads);
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"{assetName}: parsing with {threads} threads took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"{assetName}: memory occupied: {(after - before) / 1000}kB");
    }

    private static string GetAssetPath(string assetName)
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", assetName);
    }
}
