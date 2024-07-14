﻿using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
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
                var resultUnsigned = Convert.ToUInt64(bits, 2);
                if (model.FixedPointShift != 0)
                    return PerformFixedPointShift(resultUnsigned, model.FixedPointShift)
                        .ToString(CultureInfo.InvariantCulture);
                return resultUnsigned.ToString();
            case WaveDataType.SignedDecimal:
                if (!CanConvert(bits)) return "XXX";

                if (input is float or double) return input.ToString() ?? "error";
                var resultSigned = ConvertToSignedInt64(bits, model.Signal.BitWidth);
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

    public static long ConvertToSignedInt64(string bitString, int bitWidth)
    {
        if (string.IsNullOrEmpty(bitString) || bitString.Length > 64)
            throw new ArgumentException("Ungültige Eingabe: Der Bit-String muss zwischen 1 und 64 Zeichen lang sein.");

        bitString = bitString.PadLeft(64, bitString.Length < bitWidth ? '0' : bitString[0]);

        return Convert.ToInt64(bitString, 2);
    }

    public static double PerformFixedPointShift(long value, int shift)
    {
        return value / Math.Pow(2, shift);
    }

    public static double PerformFixedPointShift(ulong value, int shift)
    {
        return value / Math.Pow(2, shift);
    }

    private static string ToLiteral(string valueTextForCompiler)
    {
        return SymbolDisplay.FormatLiteral(valueTextForCompiler, false);
    }
}