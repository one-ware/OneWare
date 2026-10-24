using OneWare.Debugger.Models;

namespace OneWare.Debugger.Helpers;

public static class ValueInputParser
{
    private const int MaxBits = 64;

    public static bool TryParse(string text, NumberBase numberBase, int bits, out ulong value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (bits is < 1 or > MaxBits)
        {
            error = "This value cannot be edited.";
            return false;
        }

        var digits = text.Replace(" ", string.Empty).Replace("_", string.Empty);

        if (digits.Length == 0)
        {
            error = "Enter a value.";
            return false;
        }

        var negative = false;

        if (numberBase == NumberBase.Dec && digits[0] is '-' or '+')
        {
            negative = digits[0] == '-';
            digits = digits[1..];
        }
        else
        {
            digits = StripPrefix(digits, numberBase);
        }

        if (digits.Length == 0 || !TryAccumulate(digits, Radix(numberBase), out var magnitude))
        {
            error = $"\"{text.Trim()}\" is not a valid {Describe(numberBase)} number.";
            return false;
        }

        var range = (UInt128)1 << bits;
        var limit = negative ? range >> 1 : range - 1;

        if (magnitude > limit)
        {
            error = $"The value does not fit in {bits} bits.";
            return false;
        }

        value = (ulong)((negative ? range - magnitude : magnitude) & (range - 1));
        return true;
    }

    private static string StripPrefix(string digits, NumberBase numberBase)
    {
        var prefix = numberBase switch
        {
            NumberBase.Hex => "0x",
            NumberBase.Oct => "0o",
            NumberBase.Bin => "0b",
            _ => null
        };

        return prefix != null && digits.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? digits[prefix.Length..]
            : digits;
    }

    private static bool TryAccumulate(string digits, int radix, out UInt128 magnitude)
    {
        magnitude = 0;

        foreach (var character in digits)
        {
            var digit = DigitValue(character);
            if (digit < 0 || digit >= radix) return false;

            if (magnitude <= ulong.MaxValue) magnitude = magnitude * (uint)radix + (uint)digit;
        }

        return true;
    }

    private static int DigitValue(char character)
    {
        return character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'a' and <= 'f' => character - 'a' + 10,
            >= 'A' and <= 'F' => character - 'A' + 10,
            _ => -1
        };
    }

    private static int Radix(NumberBase numberBase)
    {
        return numberBase switch
        {
            NumberBase.Hex => 16,
            NumberBase.Oct => 8,
            NumberBase.Bin => 2,
            _ => 10
        };
    }

    private static string Describe(NumberBase numberBase)
    {
        return numberBase switch
        {
            NumberBase.Hex => "hexadecimal",
            NumberBase.Oct => "octal",
            NumberBase.Bin => "binary",
            _ => "decimal"
        };
    }
}
