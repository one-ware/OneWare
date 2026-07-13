using System.Linq;
using OneWare.Verilog.Parsing;
using Xunit;

namespace OneWare.Verilog.UnitTests;

public class VerilogNodeProviderTests
{
    [Fact]
    public void ExtractTopEntities_DetectsModuleWithParameterBlock()
    {
        const string source = """
                              module my_custom_top #(
                                  parameter DATA_WIDTH = 32,
                                  parameter ADDR_WIDTH = 8
                              )(
                                  input wire clk,
                                  input wire rst,
                                  input wire [DATA_WIDTH-1:0] data_in,
                                  output reg [DATA_WIDTH-1:0] data_out
                              );
                              endmodule
                              """;

        var names = VerilogNodeProvider.ExtractTopEntities(source).ToList();

        Assert.Contains("my_custom_top", names);
    }

    [Fact]
    public void ExtractTopEntities_DetectsModuleWithParameterBlockAndWhitespaceBeforePorts()
    {
        const string source = """
                              module my_custom_top #(
                                  parameter DATA_WIDTH = 32
                              )
                              (
                                  input wire clk,
                                  output reg data_out
                              );
                              endmodule
                              """;

        var names = VerilogNodeProvider.ExtractTopEntities(source).ToList();

        Assert.Contains("my_custom_top", names);
    }

    [Fact]
    public void ExtractTopEntities_DetectsPlainModule()
    {
        const string source = """
                              module plain_top(
                                  input wire clk,
                                  output reg out
                              );
                              endmodule
                              """;

        var names = VerilogNodeProvider.ExtractTopEntities(source).ToList();

        Assert.Contains("plain_top", names);
    }

    [Fact]
    public void ExtractNodes_ExtractsPortsFromParameterizedModule()
    {
        const string source = """
                              module my_custom_top #(
                                  parameter DATA_WIDTH = 32
                              )(
                                  input wire clk,
                                  input wire rst,
                                  output reg data_out
                              );
                              endmodule
                              """;

        var nodes = VerilogNodeProvider.ExtractNodes(source).ToList();
        var names = nodes.Select(n => n.Name).ToList();

        Assert.Contains("clk", names);
        Assert.Contains("rst", names);
        Assert.Contains("data_out", names);
    }
}
