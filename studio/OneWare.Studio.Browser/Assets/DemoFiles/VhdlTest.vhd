library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.numeric_std.all; 

      
ENTITY VHDPlus IS
PORT (
  --#IOVoltagePins
  CLK : IN STD_LOGIC;
  
  led: OUT STD_LOGIC := '0'

);
END VHDPlus;

ARCHITECTURE BEHAVIORAL OF VHDPlus IS
  
BEGIN

  --#SetIOVoltage
  PROCESS (CLK)  
    VARIABLE Thread3 : NATURAL range 0 to 6000004 := 0;
  BEGIN
  IF RISING_EDGE(CLK) THEN
    CASE (Thread3) IS
      WHEN 0 =>
        led <= '0';
        Thread3 := 1;
      WHEN 1 to 3000001 =>
        Thread3 := Thread3 + 1;
      WHEN 3000002 =>
        led <= '1';
        Thread3 := 3000003;
      WHEN 3000003 to 6000003 =>
        IF (Thread3 < 6000003) THEN 
          Thread3 := Thread3 + 1;
        ELSE
          Thread3 := 0;
        END IF;
      WHEN others => Thread3 := 0;
    END CASE;
  END IF;
  END PROCESS;
  
END BEHAVIORAL;