using System.Diagnostics;
using System.IO;
using System.Reflection;
using OneWare.VcdViewer.Models;
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
    public void ParseTest()
    {
        var sw = new Stopwatch();
        sw.Start();
        var result = VcdParser.ParseVcd(GetTestStream());
        sw.Stop();
        
        _output.WriteLine($"Parsing took {sw.ElapsedMilliseconds}ms");
        //Assert.Equal(24004, result.Changes.Count);
    }
}