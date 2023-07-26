using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

    [Fact]
    public void SignalLineTest()
    {
        var changeTimes = new List<long>()
        {
            0, 1000, 10000, 100000
        };
        var signal = new VcdSignal<byte>(changeTimes, VcdLineType.Reg, 4, '#', "Line");
        
        signal.AddChange(0, (byte)0);
        signal.AddChange(1, (byte)1);
        signal.AddChange(2, (byte)2);
        signal.AddChange(3, (byte)3);
        Assert.Equal((byte)2, signal.GetValueFromOffset(10000));
        Assert.Equal((byte)2, signal.GetValueFromOffset(10001));
        Assert.Equal(1, signal.FindIndex(1005));
        
        Assert.Equal(3, signal.FindIndex(1000000000));
        Assert.Equal((byte)3, signal.GetValueFromIndex(3));
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
        _output.WriteLine($"Memory occupied: {(after-before)/1000}kB");
        Assert.Equal(5, result.Definition.SignalRegister.Count);
        Assert.Equal(24003, result.Definition.ChangeTimes.Count);
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
        await VcdParser.ReadSignalsAsync(path, vcdFile, new Progress<(int,int)>(),
            new CancellationToken(), threads);
        sw.Stop();
        var after = GC.GetTotalMemory(true);

        _output.WriteLine($"Parsing with {threads} Threads took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after-before)/1000}kB");
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