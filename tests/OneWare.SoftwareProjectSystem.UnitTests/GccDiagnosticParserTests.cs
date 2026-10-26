using OneWare.Essentials.Enums;
using OneWare.SoftwareProjectSystem.Build;
using Xunit;

namespace OneWare.SoftwareProjectSystem.UnitTests;

public class GccDiagnosticParserTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "swproj-root"));

    [Fact]
    public void ParsesErrorWithColumn()
    {
        Assert.True(GccDiagnosticParser.TryParse("src/main.cpp:5:3: error: 'x' was not declared in this scope",
            Root, out var diagnostic));

        Assert.Equal(Path.Combine(Root, "src", "main.cpp"), diagnostic.FilePath);
        Assert.Equal(5, diagnostic.Line);
        Assert.Equal(3, diagnostic.Column);
        Assert.Equal(ErrorType.Error, diagnostic.Type);
        Assert.Equal("'x' was not declared in this scope", diagnostic.Message);
    }

    [Theory]
    [InlineData("main.c:7: warning: unused variable", ErrorType.Warning, null)]
    [InlineData("main.c:7:1: note: declared here", ErrorType.Hint, 1)]
    [InlineData("main.c:7:10: fatal error: foo.h: No such file or directory", ErrorType.Error, 10)]
    public void ParsesSeverities(string line, ErrorType type, int? column)
    {
        Assert.True(GccDiagnosticParser.TryParse(line, Root, out var diagnostic));
        Assert.Equal(type, diagnostic.Type);
        Assert.Equal(7, diagnostic.Line);
        Assert.Equal(column, diagnostic.Column);
    }

    [Fact]
    public void KeepsAbsolutePaths()
    {
        var file = Path.Combine(Root, "a.c");
        Assert.True(GccDiagnosticParser.TryParse($"{file}:1:2: error: boom", "/elsewhere", out var diagnostic));
        Assert.Equal(file, diagnostic.FilePath);
    }

    [Fact]
    public void ParsesWindowsDrivePaths()
    {
        Assert.True(GccDiagnosticParser.TryParse(@"C:\work\main.c:3:4: error: boom", Root, out var diagnostic));
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(4, diagnostic.Column);
        Assert.Equal("boom", diagnostic.Message);
    }

    [Theory]
    [InlineData("main.c:(.text+0x9): undefined reference to `foo'")]
    [InlineData("collect2: error: ld returned 1 exit status")]
    public void ReportsLinkerErrorsWithoutFile(string line)
    {
        Assert.True(GccDiagnosticParser.TryParse(line, Root, out var diagnostic));
        Assert.Null(diagnostic.FilePath);
        Assert.Equal(ErrorType.Error, diagnostic.Type);
    }

    [Theory]
    [InlineData("In file included from main.c:1:")]
    [InlineData("    5 |   int x = y;")]
    [InlineData("gcc -c main.c -o main.o")]
    public void IgnoresOtherLines(string line)
    {
        Assert.False(GccDiagnosticParser.TryParse(line, Root, out _));
    }
}
