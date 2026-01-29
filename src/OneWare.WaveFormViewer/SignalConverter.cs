using System.Globalization;
using System.Numerics;
using System.Text;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Parser.Helpers;
using OneWare.WaveFormViewer.Enums;
using OneWare.WaveFormViewer.Models;

namespace OneWare.WaveFormViewer;

public static class SignalConverter
{
    public static string ConvertSignal(object input, WaveModel model)
    {
        var bits = GetBits(input);

        if (bits == null) return input.ToString() ?? "error";

        switch (model.DataType)
        {
            case WaveDataType.Binary:
                return bits;
            case WaveDataType.Decimal:
                if (!CanConvert(bits)) return "XXX";

                if (input is float or double) return input.ToString() ?? "error";
                var resultUnsigned = ConvertToUnsignedInt(bits);
                if (model.FixedPointShift != 0)
                    return PerformFixedPointShift(resultUnsigned, model.FixedPointShift)
                        .ToString(CultureInfo.InvariantCulture);
                return resultUnsigned.ToString();
            case WaveDataType.SignedDecimal:
                if (!CanConvert(bits)) return "XXX";

                if (input is float or double) return input.ToString() ?? "error";
                var resultSigned = ConvertToSignedInt(bits);
                if (model.FixedPointShift != 0)
                    return PerformFixedPointShift(resultSigned, model.FixedPointShift)
                        .ToString(CultureInfo.InvariantCulture);
                return resultSigned.ToString();
            case WaveDataType.Hex:
                return ConvertToHexString(bits);
            case WaveDataType.Ascii:
                return ConvertToAsciiString(bits);
            default:
                return input.ToString() ?? "error";
        }
    }

    private static bool CanConvert(string bits)
    {
        return bits.All(x => x is '0' or '1');
    }

    private static string? GetBits(object input)
    {
        switch (input)
        {
            case StdLogic stdLogic:
                return StdLogicHelpers.GetChar(stdLogic);
            case StdLogic[] stdLogicArray:
                var binary = new StringBuilder();
                foreach (var b in stdLogicArray) binary.Append(StdLogicHelpers.GetChar(b));
                return binary.ToString();
            case float floatValue:
                return string.Join("",
                    BitConverter.GetBytes(floatValue).Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            case double floatValue:
                return string.Join("",
                    BitConverter.GetBytes(floatValue).Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        }

        return null;
    }

    public static string ConvertToHexString(string bitString)
    {
        if (string.IsNullOrEmpty(bitString))
            return bitString;

        var result = new StringBuilder();

        if (bitString.Length % 4 != 0) bitString = bitString.PadLeft((bitString.Length / 4 + 1) * 4, '0');

        for (var i = 0; i < bitString.Length; i += 4)
        {
            var chunk = bitString.Substring(i, 4);

            if (CanConvert(chunk))
            {
                var hexDigit = Convert.ToByte(chunk, 2).ToString("X1");
                result.Append(hexDigit);
            }
            else
            {
                var cleanedChunk = string.Join("", chunk.Where(x => x is not '0').Distinct());
                result.Append(cleanedChunk?.Length == 1 ? cleanedChunk[0] : 'X');
            }
        }

        return result.ToString();
    }

    public static string ConvertToAsciiString(string bitString)
    {
        if (string.IsNullOrEmpty(bitString))
            return bitString;

        var result = new StringBuilder();

        if (bitString.Length % 8 != 0) bitString = bitString.PadLeft((bitString.Length / 8 + 1) * 8, '0');

        for (var i = 0; i < bitString.Length; i += 8)
        {
            var chunk = bitString.Substring(i, 8);

            if (CanConvert(chunk))
            {
                var byteValue = Convert.ToByte(chunk, 2);
                result.Append((char)byteValue);
            }
            else
            {
                var cleanedChunk = string.Join("", chunk.Where(x => x is not '0').Distinct());
                result.Append(cleanedChunk?.Length == 1 ? cleanedChunk[0] : 'X');
            }
        }

        return ToLiteral(result.ToString());
    }

    public static BigInteger ConvertToSignedInt(string bitString)
    {
        if (string.IsNullOrEmpty(bitString))
            throw new ArgumentException("Invalid input: The bit string must be at least 1 bit long");
        return BigInteger.Parse(bitString, NumberStyles.AllowBinarySpecifier);
    }

    public static BigInteger ConvertToUnsignedInt(string bitString)
    {
        if (string.IsNullOrEmpty(bitString))
            throw new ArgumentException("Invalid input: The bit string must be at least 1 bit long");

        var value = BigInteger.Zero;
        foreach (var bit in bitString)
        {
            value <<= 1;
            if (bit == '1') value += 1;
        }

        return value;
    }

    public static double PerformFixedPointShift(BigInteger value, int shift)
    {
        return (double)value / Math.Pow(2, shift);
    }


    private static string ToLiteral(string input)
    {
        var sb = new StringBuilder(input.Length + 2);

        foreach (var ch in input)
            switch (ch)
            {
                case '\"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\0':
                    sb.Append("\\0");
                    break;
                case '\a':
                    sb.Append("\\a");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\v':
                    sb.Append("\\v");
                    break;
                default:
                    if (char.IsControl(ch))
                        sb.AppendFormat("\\u{0:X4}", (int)ch);
                    else
                        sb.Append(ch);
                    break;
            }

        return sb.ToString();
    }
}