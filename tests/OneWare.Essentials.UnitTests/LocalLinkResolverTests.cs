using System;
using System.IO;
using OneWare.Essentials.Helpers;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class LocalLinkResolverTests : IDisposable
{
    private readonly string _root;
    private readonly string _file;

    public LocalLinkResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "LocalLinkResolverTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "my folder"));
        _file = Path.Combine(_root, "my folder", "top level.vhd");
        File.WriteAllText(_file, "entity top is end;");
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Theory]
    [InlineData("https://one-ware.com")]
    [InlineData("http://localhost:5000/x")]
    [InlineData("mailto:info@one-ware.com")]
    [InlineData(" vscode://file/x ")]
    public void IsExternalLink_DetectsUriSchemes(string link)
    {
        Assert.True(LocalLinkResolver.IsExternalLink(link));
    }

    [Theory]
    [InlineData("file:///tmp/x.txt")]
    [InlineData("C:\\src\\main.c")]
    [InlineData("C:/src/main.c")]
    [InlineData("/home/user/main.c")]
    [InlineData("src/main.c")]
    [InlineData("main.c:12")]
    [InlineData("main.c:12:5")]
    public void IsExternalLink_TreatsPathsAsLocal(string link)
    {
        Assert.False(LocalLinkResolver.IsExternalLink(link));
    }

    [Fact]
    public void TryResolve_AbsolutePath()
    {
        Assert.True(LocalLinkResolver.TryResolve(_file, [], out var path, out var line));
        Assert.Equal(_file, path);
        Assert.Null(line);
    }

    [Fact]
    public void TryResolve_AngleBracketsAndPercentEncoding()
    {
        var link = "<" + _file.Replace(" ", "%20") + ">";
        Assert.True(LocalLinkResolver.TryResolve(link, [], out var path, out _));
        Assert.Equal(_file, path);
    }

    [Fact]
    public void TryResolve_FileUri()
    {
        var link = new Uri(_file).AbsoluteUri;
        Assert.True(LocalLinkResolver.TryResolve(link, [], out var path, out _));
        Assert.Equal(_file, path);
    }

    [Theory]
    [InlineData(":12", 12)]
    [InlineData(":7:3", 7)]
    [InlineData("#L42", 42)]
    [InlineData("#L5-L9", 5)]
    public void TryResolve_LineSuffix(string suffix, int expectedLine)
    {
        Assert.True(LocalLinkResolver.TryResolve(_file + suffix, [], out var path, out var line));
        Assert.Equal(_file, path);
        Assert.Equal(expectedLine, line);
    }

    [Fact]
    public void TryResolve_RelativePathUsesFirstMatchingBaseDirectory()
    {
        var bases = new[] { null, Path.Combine(_root, "missing"), _root };
        Assert.True(LocalLinkResolver.TryResolve("my folder/top level.vhd:3", bases, out var path, out var line));
        Assert.Equal(_file, path);
        Assert.Equal(3, line);
    }

    [Fact]
    public void TryResolve_Directory()
    {
        Assert.True(LocalLinkResolver.TryResolve("my%20folder", [_root], out var path, out var line));
        Assert.Equal(Path.Combine(_root, "my folder"), path);
        Assert.Null(line);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#heading")]
    [InlineData("https://one-ware.com")]
    [InlineData("does-not-exist.vhd")]
    [InlineData("my folder/does-not-exist.vhd:12")]
    public void TryResolve_ReturnsFalseForUnresolvableLinks(string link)
    {
        Assert.False(LocalLinkResolver.TryResolve(link, [_root], out _, out _));
    }
}
