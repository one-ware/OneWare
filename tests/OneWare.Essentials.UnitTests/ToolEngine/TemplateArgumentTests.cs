using System;

namespace OneWare.Essentials.UnitTests.ToolEngine;

using System.Runtime.InteropServices;
using OneWare.Essentials.ToolEngine;
using Xunit;


public class TemplateArgumentTests
{
    [Fact]
    public void GetArgument_ShouldReplacePlaceholders()
    {
        const string template = "gcc -o OUTPUT INPUT";
        var mappings = new[]
        {
            ("OUTPUT", "main.exe", false),
            ("INPUT", "main.c", false)
        };
        var arg = new TemplateArgument(template, mappings);
        arg.Prepare(OSPlatform.Windows);
        
        var result = arg.GetArgument();
        
        Assert.Equal("gcc -o main.exe main.c", result);
    }

    [Theory]
    [InlineData("C:/Users/Test", @"C:\Users\Test")] 
    public void Prepare_ShouldAdjustPathsForWindows(string inputPath, string expectedPath)
    {
        const string template = "path: PATH";
        var arg = new TemplateArgument(template, ("PATH", inputPath, true));
        
        arg.Prepare(OSPlatform.Windows);
        var result = arg.GetArgument();
        
        Assert.Contains(expectedPath, result);
    }

    [Theory]
    [InlineData(@"C:\Users\Test", "C:/Users/Test")]
    public void Prepare_ShouldAdjustPathsForLinux(string inputPath, string expectedPath)
    {
        const string template = "path: PATH";
        var arg = new TemplateArgument(template, ("PATH", inputPath, true));
        
        arg.Prepare(OSPlatform.Linux);
        var result = arg.GetArgument();
        
        Assert.Contains(expectedPath, result);
    }

    [Fact]
    public void Prepare_WithPathMapper_ShouldTransformValue()
    {
        const string template = "file: FILE";
        var arg = new TemplateArgument(template, ("FILE", "relative/path", true));

        arg.Prepare(OSPlatform.Linux, Mapper);
        var result = arg.GetArgument();
        
        Assert.Equal("file: /base/relative/path", result);
        return;

        string Mapper(string p) => $"/base/{p}";
    }
}