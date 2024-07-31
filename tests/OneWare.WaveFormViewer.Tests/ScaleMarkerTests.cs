using System.Collections.Generic;
using System.Linq;
using OneWare.WaveFormViewer.Controls;
using Xunit;

namespace OneWare.WaveFormViewer.Tests;

public class ScaleMarkerTests
{
    [Fact]
    public void CalculateMarkerTest()
    {
        var result = WaveFormScale.CalculateMarkers(0, 250, 6)
            .Where(x => x.IsMainMarker)
            .Select(x => x.Position);
        
        Assert.Equal([0, 50, 100, 150, 200, 250], result);
    }
    
    [Fact]
    public void CalculateMarkerTest2()
    {
        var result = WaveFormScale.CalculateMarkers(4900, 25999, 6)
            .Where(x => x.IsMainMarker)
            .Select(x => x.Position);
        
        Assert.Equal([5000, 10000, 15000, 20000, 25000], result);
    }
}