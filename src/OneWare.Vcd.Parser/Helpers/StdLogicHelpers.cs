using OneWare.Vcd.Parser.Data;

namespace OneWare.Vcd.Parser.Helpers;

public static class StdLogicHelpers
{
    public static StdLogic ParseLogic(char value)
    {
        return value switch
        {
            'U' => StdLogic.U,
            'X' => StdLogic.X,
            '0' => StdLogic.Zero,
            '1' => StdLogic.Full,
            'Z' => StdLogic.Z,
            'W' => StdLogic.W,
            'L' => StdLogic.L,
            'H' => StdLogic.H,
            '-' => StdLogic.DontCare,
            _ => StdLogic.DontCare
        };
    }

    public static string GetChar(StdLogic logic)
    {
        return logic switch
        {
            StdLogic.Zero => "0",
            StdLogic.Full => "1",
            StdLogic.U => "U",
            StdLogic.X => "X",
            StdLogic.Z => "Z",
            StdLogic.W => "W",
            StdLogic.L => "L",
            StdLogic.H => "H",
            StdLogic.DontCare => "-",
            _ => "-"
        };
    }
}