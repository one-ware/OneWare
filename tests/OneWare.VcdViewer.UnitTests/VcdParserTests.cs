using System.Diagnostics;
using System.IO;
using System.Reflection;
using OneWare.VcdViewer.Parser;
using Xunit;
using Xunit.Abstractions;

namespace OneWare.VcdViewer.UnitTests;
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
        return assembly.GetManifestResourceStream("OneWare.VcdViewer.UnitTests.Assets.GHDL.vcd")!;
    }
    
        
    [Fact]
    public void ParseTest100()
    {
        var sw = new Stopwatch();
        sw.Start();
        VcdParser.ParseVcd(GetTestStream(), 1f);
        sw.Stop();
        
        _output.WriteLine($"Parsing with 100% accuracy took {sw.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public void ParseTest50()
    {
        var sw = new Stopwatch();
        sw.Start();
        VcdParser.ParseVcd(GetTestStream(), 0.5f);
        sw.Stop();
        
        _output.WriteLine($"Parsing with 50% accuracy took {sw.ElapsedMilliseconds}ms");
    }
}