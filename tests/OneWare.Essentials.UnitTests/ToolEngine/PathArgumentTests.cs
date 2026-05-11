using System.Runtime.InteropServices;
using OneWare.Essentials.ToolEngine;
using Xunit;

namespace OneWare.Essentials.UnitTests.ToolEngine;

public class PathArgumentTests
{
    [Theory]
    [InlineData("folder/subfolder/file.txt", @"folder\subfolder\file.txt")]
    [InlineData("C:/Data/Test", @"C:\Data\Test")]
    public void Prepare_ShouldConvertToWindowsBackslashes(string input, string expected)
    {
        var arg = new PathArgument(input);
        
        arg.Prepare(OSPlatform.Windows);
        var result = arg.GetArgument();
        
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(@"folder\subfolder\file.txt", "folder/subfolder/file.txt")]
    [InlineData("/home/user/test", "/home/user/test")]
    public void Prepare_ShouldConvertToLinuxForwardSlashes(string input, string expected)
    {
        var arg = new PathArgument(input);
        
        arg.Prepare(OSPlatform.Linux);
        var result = arg.GetArgument();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Prepare_WithMapper_ShouldApplyTransformationBeforeNormalization()
    {
        const string initialPath = "relative/path";
        var arg = new PathArgument(initialPath);

        arg.Prepare(OSPlatform.Windows, Mapper);
        Assert.Equal(@"\abs\root\relative\path", arg.GetArgument());
        return;

        string Mapper(string p) => $"/abs/root/{p}";
    }

    [Fact]
    public void GetArgument_WithoutPrepare_ShouldReturnInitialPath()
    {
        const string initialPath = "some/path/here";
        var arg = new PathArgument(initialPath);
        
        var result = arg.GetArgument();
        
        Assert.Equal(initialPath, result);
    }
}