module %PROJECTNAME% (
    input clk,
    output led
);

reg [23:0] counter = 0;

always @ (posedge clk)
begin
    counter <= counter + 1'b1;
end

assign led = counter[20];

endmodule
