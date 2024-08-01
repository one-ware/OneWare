using System;
using System.IO;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using Xunit;

namespace OneWare.UniversalFpgaProjectSystem.Tests;

public class LoadFpgaTests
{
    [Fact]
    public void LoadFpgaTest()
    {
        var package = new GenericFpgaPackage("CYC5000", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Cyc5000/fpga.json"));
        
        Assert.Equal("CYC5000", package.Name);
    }
}