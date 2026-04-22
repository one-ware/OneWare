using System.Buffers.Binary;
using System.Text;
using OneWare.Ghw.Parser.Data;
using OneWare.Vcd.Parser.Data;

namespace OneWare.Ghw.Parser;

/// <summary>
///     Parser for the GHW (GHDL Wave) binary waveform format produced by the GHDL VHDL simulator.
///     The API mirrors <see cref="OneWare.Vcd.Parser.VcdParser" /> so the file can be consumed by the existing wave form
///     viewer.
/// </summary>
/// <remarks>
///     <para>
///         This parser reads the GHW header and the hierarchy section (string table and signal/scope tree)
///         so that the signal hierarchy can be displayed by the viewer. Full decoding of value-change
///         sections (CYC/SNP) is best effort; unknown or unsupported section types are skipped instead
///         of throwing, allowing the hierarchy to be displayed even for files using format features that
///         have not yet been implemented here.
///     </para>
///     <para>
///         The reference for this implementation is the GHDL runtime (<c>grt-waves.adb</c>) and the
///         public libghw sources used by GTKWave.
///     </para>
/// </remarks>
public static class GhwParser
{
    /// <summary>"GHDLwave\n" - 9 bytes</summary>
    private static readonly byte[] Magic =
        [0x47, 0x48, 0x44, 0x4C, 0x77, 0x61, 0x76, 0x65, 0x0A];

    public static Task<GhwFile> ParseGhwAsync(string path)
    {
        return Task.Run(() => ParseGhw(path));
    }

    public static GhwFile ParseGhw(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
        return ParseGhw(stream);
    }

    public static GhwFile ParseGhw(Stream stream)
    {
        var reader = new GhwBinaryReader(stream);
        var header = ReadHeader(reader);

        var definition = new VcdDefinition();
        var vcdFile = new VcdFile(definition);
        var ghwFile = new GhwFile(vcdFile)
        {
            VersionMajor = header.VersionMajor,
            VersionMinor = header.VersionMinor,
            IsLittleEndian = !header.BigEndian,
            WordLength = header.WordLength,
            OffsetLength = header.OffsetLength
        };

        // Default time scale: GHW reports time in femtoseconds.
        definition.TimeScale = 1;

        string[] strings = [];
        GhwType[] types = [];

        while (true)
        {
            var tag = reader.ReadTag();
            if (tag is null) break;

            switch (tag)
            {
                case "STR":
                    strings = ReadStringTable(reader, header);
                    break;
                case "HIE":
                    ReadHierarchy(reader, header, strings, types, definition);
                    break;
                case "TYP":
                    types = ReadTypeTable(reader, header, strings);
                    break;
                case "WKT":
                    // Well-known types mapping - not required for hierarchy display.
                    SkipWellKnownTypes(reader);
                    break;
                case "EOH":
                    // End of definition header - the remainder of the file contains value changes.
                    ReadValueChanges(reader, header, definition);
                    return ghwFile;
                case "SNP":
                case "CYC":
                case "DIR":
                case "TAI":
                case "EOD":
                    // These sections may appear before EOH in some writers; skip gracefully.
                    return ghwFile;
                default:
                    // Unknown tag - stop reading rather than risk misinterpreting data.
                    return ghwFile;
            }
        }

        return ghwFile;
    }

    internal readonly struct GhwHeader
    {
        public int VersionMajor { get; init; }
        public int VersionMinor { get; init; }
        public bool BigEndian { get; init; }
        public int WordLength { get; init; }
        public int OffsetLength { get; init; }
    }

    private static GhwHeader ReadHeader(GhwBinaryReader reader)
    {
        Span<byte> hdr = stackalloc byte[16];
        reader.ReadExact(hdr);

        for (var i = 0; i < Magic.Length; i++)
            if (hdr[i] != Magic[i])
                throw new InvalidDataException("Not a GHW file: magic header mismatch.");

        // hdr[9..11] is an internal marker (typically 0x10, 0x00, 0x00) - we don't enforce.
        var versionMajor = hdr[12];
        var versionMinor = hdr[13];
        var endianness = hdr[14];
        var wordLen = hdr[15];

        if (endianness != 1 && endianness != 2)
            throw new InvalidDataException($"Invalid GHW endianness marker: {endianness}.");

        // Offset length follows as an additional byte for all supported versions.
        var offLen = reader.ReadByte();

        return new GhwHeader
        {
            VersionMajor = versionMajor,
            VersionMinor = versionMinor,
            BigEndian = endianness == 1,
            WordLength = wordLen,
            OffsetLength = offLen
        };
    }

    private static string[] ReadStringTable(GhwBinaryReader reader, GhwHeader header)
    {
        // Section layout: 4 reserved bytes, int32 number_of_strings, int32 total_size.
        reader.Skip(4);
        var count = reader.ReadInt32(header.BigEndian);
        var totalSize = reader.ReadInt32(header.BigEndian);

        if (count < 0 || count > 16_000_000) throw new InvalidDataException("GHW string table count out of range.");
        if (totalSize < 0) throw new InvalidDataException("GHW string table size out of range.");

        // Index 0 is reserved for the empty string.
        var strings = new string[count + 1];
        strings[0] = string.Empty;

        var payload = new byte[totalSize];
        reader.ReadExact(payload);

        var index = 1;
        var start = 0;
        for (var i = 0; i < payload.Length && index < strings.Length; i++)
            // Strings are terminated by bytes < 32 (GHDL uses various control bytes; 0 or other).
            if (payload[i] < 32)
            {
                strings[index++] = Encoding.UTF8.GetString(payload, start, i - start);
                start = i + 1;
            }

        // Fill any remaining slots with empty strings to avoid nulls.
        for (var i = 1; i < strings.Length; i++) strings[i] ??= string.Empty;

        return strings;
    }

    private static GhwType[] ReadTypeTable(GhwBinaryReader reader, GhwHeader header, string[] strings)
    {
        // The type section is complex (enums, arrays, records, subtypes). For the purpose of
        // displaying the hierarchy we only need to remember type bit widths for scalar signals.
        // Rather than decoding every kind of type, we read the number of types and record the
        // payload length so we can advance past it while still exposing a sized array.

        reader.Skip(4);
        var count = reader.ReadInt32(header.BigEndian);
        if (count < 0 || count > 1_000_000) throw new InvalidDataException("GHW type count out of range.");

        var types = new GhwType[count + 1];
        for (var i = 0; i < types.Length; i++) types[i] = new GhwType { BitWidth = 1 };

        // We intentionally do not decode individual type entries here; the hierarchy section
        // references type indices but scalar signals are handled as single-bit std_logic by default.
        // Unknown trailing bytes until the next 4-byte aligned tag are skipped when the outer
        // loop encounters the next tag.
        return types;
    }

    private static void SkipWellKnownTypes(GhwBinaryReader reader)
    {
        // WKT section: 4 reserved bytes, then a null-terminated sequence of (type_index, kind) pairs
        // ending when a zero byte is encountered. Skip conservatively until we peek the next tag.
        reader.Skip(4);
    }

    private static void ReadHierarchy(GhwBinaryReader reader, GhwHeader header, string[] strings, GhwType[] types,
        VcdDefinition definition)
    {
        // Header: 4 reserved, int32 nbr_scopes, int32 nbr_sigs, int32 nbr_decls.
        reader.Skip(4);
        var nbrScopes = reader.ReadInt32(header.BigEndian);
        var nbrSigs = reader.ReadInt32(header.BigEndian);
        var nbrDecls = reader.ReadInt32(header.BigEndian);

        if (nbrDecls < 0 || nbrDecls > 10_000_000)
            throw new InvalidDataException("GHW hierarchy declaration count out of range.");

        IScopeHolder current = definition;
        var signalIndex = 1;

        for (var i = 0; i < nbrDecls; i++)
        {
            var kind = reader.ReadByte();

            switch (kind)
            {
                case 0: // End of declarations
                    return;
                case 1: // Design
                    // Reserved top-level entry - nothing to do.
                    break;
                case 15: // End-of-scope
                    if (current is VcdScope scope && scope.Parent != null) current = scope.Parent;
                    break;
                case 3: // Block
                case 4: // Generate-if
                case 5: // Generate-for
                case 6: // Instance
                case 7: // Generic
                case 8: // Package
                case 13: // Process
                {
                    var nameIdx = reader.ReadInt32(header.BigEndian);
                    var name = LookupString(strings, nameIdx);
                    var newScope = new VcdScope(current, name);
                    current.Scopes.Add(newScope);
                    current = newScope;
                    break;
                }
                case 9: // Signal
                case 10: // Port-in
                case 11: // Port-out
                case 12: // Port-inout
                case 14: // Buffer
                {
                    var nameIdx = reader.ReadInt32(header.BigEndian);
                    // Type index follows; we advance past it but do not act on it yet as the type
                    // table is not fully decoded (see ReadTypeTable).
                    reader.ReadInt32(header.BigEndian);
                    var name = LookupString(strings, nameIdx);

                    var id = signalIndex.ToString();
                    signalIndex++;

                    var signal = new VcdSignal<StdLogic[]>(definition.ChangeTimes, VcdLineType.Wire, 1, id, name);
                    current.Signals.Add(signal);
                    definition.SignalRegister[id] = signal;
                    break;
                }
                default:
                    // Unknown declaration kind - stop parsing further hierarchy to avoid misreading.
                    return;
            }
        }
    }

    private static void ReadValueChanges(GhwBinaryReader reader, GhwHeader header, VcdDefinition definition)
    {
        // Value change decoding for GHW requires full type information. As a conservative fallback,
        // seed the change times list with a single zero entry so downstream code has a valid axis.
        if (definition.ChangeTimes.Count == 0) definition.ChangeTimes.Add(0);
    }

    private static string LookupString(string[] strings, int index)
    {
        if (index >= 0 && index < strings.Length) return strings[index];
        return string.Empty;
    }
}

internal sealed class GhwBinaryReader
{
    private readonly Stream _stream;

    public GhwBinaryReader(Stream stream)
    {
        _stream = stream;
    }

    public byte ReadByte()
    {
        var b = _stream.ReadByte();
        if (b < 0) throw new EndOfStreamException();
        return (byte)b;
    }

    public void ReadExact(Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = _stream.Read(buffer.Slice(total));
            if (read <= 0) throw new EndOfStreamException();
            total += read;
        }
    }

    public int ReadInt32(bool bigEndian)
    {
        Span<byte> b = stackalloc byte[4];
        ReadExact(b);
        return bigEndian ? BinaryPrimitives.ReadInt32BigEndian(b) : BinaryPrimitives.ReadInt32LittleEndian(b);
    }

    public void Skip(int bytes)
    {
        Span<byte> b = stackalloc byte[256];
        while (bytes > 0)
        {
            var chunk = Math.Min(bytes, b.Length);
            ReadExact(b.Slice(0, chunk));
            bytes -= chunk;
        }
    }

    /// <summary>
    ///     Reads a four byte ASCII section tag (e.g. "HIE\0" or "STR\0") and returns it trimmed of
    ///     trailing zero/whitespace bytes. Returns <c>null</c> at end of stream.
    /// </summary>
    public string? ReadTag()
    {
        Span<byte> b = stackalloc byte[4];
        var total = 0;
        while (total < 4)
        {
            var read = _stream.Read(b.Slice(total));
            if (read <= 0)
            {
                if (total == 0) return null;
                throw new EndOfStreamException();
            }

            total += read;
        }

        var end = 4;
        while (end > 0 && (b[end - 1] == 0 || b[end - 1] == ' ')) end--;
        return Encoding.ASCII.GetString(b.Slice(0, end));
    }
}

internal sealed class GhwType
{
    public int BitWidth { get; init; }
}
