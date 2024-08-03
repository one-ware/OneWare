`timescale 1 ns/1 ps

module %PROJECTNAME%_tb;

    reg clk = 0;
    wire led;

    %PROJECTNAME% UUT (.clk(clk), .led(led));

    initial begin
        $dumpfile("%PROJECTNAME%_tb.vcd");
        $dumpvars(0, %PROJECTNAME%_tb);

        forever #41.666 clk = !clk;
    end

    initial begin
        #1000000;
        $finish;
    end

endmodule
