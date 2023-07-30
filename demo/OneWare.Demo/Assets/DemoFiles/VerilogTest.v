module HELLO_WORLD(); // module doesn't have input or outputs
  initial begin
    $display('Hello World');
    $finish; // stop the simulator
  end
endmodule