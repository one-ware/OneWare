using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
    
    private static Stream GetTestStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream("OneWare.Vcd.Parser.UnitTests.Assets.GHDL.vcd")!;
    }

    [Fact]
    public void ParseTest()
    {
        var sw = new Stopwatch();

        var before = GC.GetTotalMemory(true);
        sw.Start();
        var result = VcdParser.ParseVcd(GetTestStream());
        sw.Stop();
        var after = GC.GetTotalMemory(true);
        
        _output.WriteLine($"Parsing took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Memory occupied: {(after-before)/1000}kB");
        Assert.Equal(5, result.Definition.SignalRegister.Count);
        //Assert.Equal(24002, (result.Definition.SignalRegister['#'] as VcdChange<bool>).Changes.Count);
    }
}