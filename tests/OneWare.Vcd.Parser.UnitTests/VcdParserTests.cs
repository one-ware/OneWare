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

    private static string GetTestPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/GHDL.vcd");
    }

    private static string GetTestPath2()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/tb_scale_bias_correction.vcd");
    }

    private static string GetTestPath3()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/tb_top_aurora.vcd");
    }

    private static string GetTestPath4()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/core.vcd");
    }
    
    private static string GetTestPath5()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/trace.vcd");
    }
    
    private static string GetTestPath6()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/integer_width.vcd");
    }
    
    [Fact]
    public void SignalLineTest()
    {
        var changeTimes = new List<long>
        {
            0, 1000, 10000, 100000
        };
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
        var changeTimes = new List<long>
        {
            0, 1000, 10000, 100000
        };
        var signal = new VcdSignal<StdLogic[]>(changeTimes, VcdLineType.Reg, 4, "#", "Line");

        var testValue = new[] { StdLogic.Full, StdLogic.X };

        signal.AddChange(0, testValue);

        Assert.Equal(testValue, signal.GetValueFromOffset(0));
    }

    [Fact]
    public void ParseTest()
    {
        var sw = new Stopwatch();

        var before = GC.GetTotalMemory(true);
        sw.Start();
        var result = VcdParser.ParseVcd(GetTestPath());
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"Parsing took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after - before) / 1000}kB");
        Assert.Equal(5, result.Definition.SignalRegister.Count);
        Assert.Equal(24004, result.Definition.ChangeTimes.Count);
    }

    [Fact]
    public void ParseTest2()
    {
        var sw = new Stopwatch();

        var before = GC.GetTotalMemory(true);
        sw.Start();
        var result = VcdParser.ParseVcd(GetTestPath2());
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"Parsing took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after - before) / 1000}kB");
        Assert.Equal(37, result.Definition.SignalRegister.Count);
        Assert.Equal(42, result.Definition.ChangeTimes.Count);
    }

    [Fact]
    public void ParseTest3()
    {
        var sw = new Stopwatch();

        var before = GC.GetTotalMemory(true);
        sw.Start();
        var result = VcdParser.ParseVcd(GetTestPath3());
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"Parsing took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after - before) / 1000}kB");
        Assert.Equal(216, result.Definition.SignalRegister.Count);
        Assert.Equal(130, result.Definition.ChangeTimes.Count);
    }
    
    [Fact]
    public void ParseTest4()
    {
        var sw = new Stopwatch();

        var before = GC.GetTotalMemory(true);
        sw.Start();
        var result = VcdParser.ParseVcd(GetTestPath4());
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"Parsing took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after - before) / 1000}kB");
    }
    
    [Fact]
    public void ParseTest6()
    {
        var result = VcdParser.ParseVcd(GetTestPath6());
        var signal = result.GetSignal("!");
        Assert.Equal(32, signal.BitWidth);
        var expected = Enumerable.Repeat(StdLogic.Zero, 31)
            .Append(StdLogic.Full)
            .ToList();
        Assert.Equal(expected, signal.GetValueFromOffset(0));
    }


    [Fact]
    public void ParseTest5()
    {
        var sw = new Stopwatch();

        var before = GC.GetTotalMemory(true);
        sw.Start();
        var result = VcdParser.ParseVcd(GetTestPath5());
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"Parsing took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after - before) / 1000}kB");
    }
    
    [Fact]
    public async Task FindLastTimeTest()
    {
        var lastTime = await VcdParser.TryFindLastTime(GetTestPath());

        Assert.Equal(1000079333000, lastTime);
    }

    private async Task ParseMulticore(string path, int threads)
    {
        var sw = new Stopwatch();
        var vcdFile = VcdParser.ParseVcdDefinition(path);

        var before = GC.GetTotalMemory(true);
        sw.Start();
        await VcdParser.ReadSignalsAsync(path, vcdFile, new Progress<(int, int)>(),
            new CancellationToken(), threads);
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"Parsing with {threads} Threads took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after - before) / 1000}kB");
    }

    [Fact]
    public async Task ParseTestMulticore()
    {
        await ParseMulticore(GetTestPath(), 1);
        await ParseMulticore(GetTestPath(), 2);
        await ParseMulticore(GetTestPath(), 4);
        await ParseMulticore(GetTestPath(), 8);
    }
}