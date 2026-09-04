using System.Globalization;
using OneWare.Debugger.Models;

namespace OneWare.Debugger.Helpers;

// Schreibt die rohen Wortwerte des Ziels in der vom Benutzer gewaehlten Schreibweise.
// Vorzeichenbehaftet heisst hier immer Zweierkomplement: der SVNR rechnet nachweislich so
// (alu_n.vhd liest die Operanden als to_integer(signed(...)) und schreibt das Ergebnis als
// to_signed(var_c, 16), beides numeric_std und damit per IEEE 1076.3 Zweierkomplement; der
// Bereich -32768..32767 ist dessen asymmetrische Signatur). Eine Wahl zwischen Einer- und
// Zweierkomplement gibt es deshalb bewusst nicht -> sie koennte nur Werte erzeugen, die die
// Maschine nie hatte.
// Ein Vorzeichen hat ausserdem nur die Dezimalanzeige. Hex, Oktal und Binaer zeigen das rohe
// Bitmuster, und darin stehen dieselben Bits, ob man sie als -1 oder als 65535 liest.
// Was sich nicht als Zahl lesen laesst - "target running", "unreadable", eine Struktur -,
// geht unveraendert durch.
public static class ValueFormatter
{
    private const int MaxBits = 64;

    // Ein einzelner Hexwert des Backends, etwa ein Register als "0x00000000".
    public static string FormatHexValue(string raw, NumberBase numberBase, bool signed)
    {
        // Hex ist die Form, in der die Werte ohnehin ankommen -> unveraendert lassen, damit die
        // Anzeige im Normalfall genau so aussieht wie zuvor, samt "0x" und Auffuellung.
        if (numberBase == NumberBase.Hex) return raw;

        return TryParseHex(raw, out var value, out var bits)
            ? Format(value, bits, numberBase, signed)
            : raw;
    }

    // Mehrere Einheiten hintereinander, durch Leerzeichen getrennt, wie der Memory-Reiter sie
    // aus den gelesenen Bytes zusammensetzt. Scheitert eine, bleibt die ganze Zeile stehen ->
    // eine halb umgerechnete Zeile waere schlechter lesbar als die rohe.
    public static string FormatHexUnits(string raw, NumberBase numberBase, bool signed)
    {
        if (numberBase == NumberBase.Hex) return raw;

        var units = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (units.Length == 0) return raw;

        var formatted = new List<string>(units.Length);

        foreach (var unit in units)
        {
            if (!TryParseHex(unit, out var value, out var bits)) return raw;

            formatted.Add(Format(value, bits, numberBase, signed));
        }

        return string.Join(' ', formatted);
    }

    // Was GDB aus dem DWARF-Typ gemacht hat, in aller Regel eine vorzeichenbehaftete
    // Dezimalzahl. Die Breite steht dort nicht mit drin und kommt deshalb vom Ziel.
    public static string FormatDecimalValue(string raw, int bits, NumberBase numberBase, bool signed)
    {
        if (bits is < 1 or > MaxBits) return raw;

        if (!long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return raw;

        return Format(Mask(unchecked((ulong)parsed), bits), bits, numberBase, signed);
    }

    // Die Breite steht in der Zahl selbst: GDB fuellt auf die Registerbreite auf, und der
    // Memory-Reiter gruppiert vorher auf die Wortbreite des Ziels. Damit bleibt ein
    // 32-Bit-Register auch dann richtig vorzeichenbehaftet, wenn das Ziel 16-Bit-Worte
    // adressiert - eine feste Wortbreite waere hier falsch.
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

    private static string Format(ulong value, int bits, NumberBase numberBase, bool signed)
    {
        var pattern = unchecked((long)value);

        return numberBase switch
        {
            NumberBase.Dec => signed
                ? SignedDecimal(value, bits)
                : value.ToString(CultureInfo.InvariantCulture),

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

    // Zweierkomplement: ist das oberste Bit gesetzt, liegt der Wert unter null, und sein Betrag
    // ist der Abstand zur naechsten Zweierpotenz.
    // Von Hand und nicht ueber einen Cast nach short/int/long, weil die Breite hier nicht immer
    // 8, 16, 32 oder 64 ist: sie kommt aus der Stellenzahl des Hexwerts, und GDB liefert auch
    // Werte wie "0xfff" -> zwoelf Bit.
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
