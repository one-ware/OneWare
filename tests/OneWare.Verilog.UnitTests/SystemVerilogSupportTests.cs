using AvaloniaEdit.Document;
using OneWare.Verilog.Indentation;
using Xunit;

namespace OneWare.Verilog.UnitTests;

public class SystemVerilogSupportTests
{
    [Fact]
    public void SupportedExtensions_IncludeSourceAndHeaderFiles()
    {
        Assert.Contains(".v", VerilogModule.VerilogExtensions);
        Assert.Contains(".vh", VerilogModule.VerilogExtensions);
        Assert.Contains(".sv", VerilogModule.SystemVerilogExtensions);
        Assert.Contains(".svh", VerilogModule.SystemVerilogExtensions);
    }

    [Fact]
    public void Indentation_HandlesConstraintBraces()
    {
        var document = new TextDocument("constraint valid {\nvalue inside {[0:7]};\n}\n");
        var strategy = new VerilogIndentationStrategy { IndentationString = "    " };

        strategy.IndentLines(document, 1, document.LineCount);

        Assert.Equal("constraint valid {\n    value inside {[0:7]};\n}\n", document.Text);
    }

    [Fact]
    public void Indentation_HandlesRandcase()
    {
        var document = new TextDocument("randcase\n1: value = 0;\nendcase\n");
        var strategy = new VerilogIndentationStrategy { IndentationString = "    " };

        strategy.IndentLines(document, 1, document.LineCount);

        Assert.Equal("randcase\n    1: value = 0;\nendcase\n", document.Text);
    }
}
