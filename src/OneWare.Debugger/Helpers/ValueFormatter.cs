using System.Globalization;
using OneWare.Debugger.Models;

namespace OneWare.Debugger.Helpers;

// Writes the target's raw word values in the notation chosen by the user.
// Signed always means two's complement here. A choice between ones' and two's
// complement deliberately does not exist -> on a two's complement machine it could
// only produce values the machine never had.
// Besides, only the decimal display has a sign. Hex, octal and binary show the raw
// bit pattern, and that holds the same bits whether they are read as -1 or as 65535.
// Whatever cannot be read as a number - "target running", "unreadable", a structure -
// passes through unchanged.
public static class ValueFormatter
{
    private const int MaxBits = 64;

    // A single hex value from the backend, such as a register as "0x00000000".
    public static string FormatHexValue(string raw, NumberBase numberBase)
    {
        // Hex is the form in which the values arrive anyway -> leave unchanged, so that the
        // display normally looks exactly as before, including "0x" and padding.
        if (numberBase == NumberBase.Hex) return raw;

        return TryParseHex(raw, out var value, out var bits)
            ? Format(value, bits, numberBase)
            : raw;
    }

    // Several units in a row, separated by spaces, as the Memory tab assembles them
    // from the bytes read. If one fails, the whole line stays as it is ->
    // a half-converted line would be harder to read than the raw one.
    public static string FormatHexUnits(string raw, NumberBase numberBase)
    {
        if (numberBase == NumberBase.Hex) return raw;

        var units = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (units.Length == 0) return raw;

        var formatted = new List<string>(units.Length);

        foreach (var unit in units)
        {
            if (!TryParseHex(unit, out var value, out var bits)) return raw;

            formatted.Add(Format(value, bits, numberBase));
        }

        return string.Join(' ', formatted);
    }

    // What GDB made of the DWARF type, as a rule a signed
    // decimal number. The width is not included there and therefore comes from the target.
    public static string FormatDecimalValue(string raw, int bits, NumberBase numberBase)
    {
        if (bits is < 1 or > MaxBits) return raw;

        if (!long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return raw;

        return Format(Mask(unchecked((ulong)parsed), bits), bits, numberBase);
    }

    public static int HexBits(string raw)
    {
        return TryParseHex(raw, out _, out var bits) ? bits : 0;
    }

    // The width is in the number itself: GDB pads to the register width, and the
    // Memory tab groups to the target's word width beforehand. That keeps a
    // 32-bit register correctly signed even when the target addresses 16-bit
    // words - a fixed word width would be wrong here.
    private static bool TryParseHex(string raw, out ulong value, out int bits)
    {
        value = 0;
        bits = 0;

        var text = raw.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];

        if (text.Length == 0 || text.Length > MaxBits / 4) return false;

        if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return false;

        bits = text.Length * 4;
        return true;
    }

    private static string Format(ulong value, int bits, NumberBase numberBase)
    {
        var pattern = unchecked((long)value);

        return numberBase switch
        {
            NumberBase.Dec => SignedDecimal(value, bits),

            NumberBase.Oct => Convert.ToString(pattern, 8).PadLeft(OctalDigits(bits), '0'),

            NumberBase.Bin => GroupNibbles(Convert.ToString(pattern, 2).PadLeft(bits, '0')),

            _ => value.ToString("X" + HexDigits(bits), CultureInfo.InvariantCulture)
        };
    }

    private static int HexDigits(int bits)
    {
        return (bits + 3) / 4;
    }

    private static int OctalDigits(int bits)
    {
        return (bits + 2) / 3;
    }

    private static string GroupNibbles(string digits)
    {
        var groups = Enumerable.Range(0, (digits.Length + 3) / 4)
            .Select(i => digits.Substring(i * 4, Math.Min(4, digits.Length - i * 4)));

        return string.Join(' ', groups);
    }

    // Two's complement: if the top bit is set, the value is below zero, and its magnitude
    // is the distance to the next power of two.
    // By hand and not through a cast to short/int/long, because the width here is not always
    // 8, 16, 32 or 64: it comes from the number of digits of the hex value, and GDB also delivers
    // values such as "0xfff" -> twelve bits.
    private static string SignedDecimal(ulong value, int bits)
    {
        if ((value & (1UL << (bits - 1))) == 0) return value.ToString(CultureInfo.InvariantCulture);

        var magnitude = bits >= MaxBits ? unchecked(0UL - value) : (1UL << bits) - value;

        return "-" + magnitude.ToString(CultureInfo.InvariantCulture);
    }

    private static ulong Mask(ulong value, int bits)
    {
        return bits >= MaxBits ? value : value & ((1UL << bits) - 1);
    }
}
