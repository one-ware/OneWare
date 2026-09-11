using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
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
        var signalTypeById = new Dictionary<int, int>();
        var wellKnownTypes = new GhwWellKnownTypes();
        var declaredSnapshotValueCount = 0;

        if (stream.CanSeek)
        {
            ParseByTagScan(stream, header, definition, ref strings, ref types, signalTypeById, ref wellKnownTypes,
                ref declaredSnapshotValueCount);
            return ghwFile;
        }

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
                    ReadHierarchy(reader, header, strings, types, definition, signalTypeById,
                        ref declaredSnapshotValueCount);
                    break;
                case "TYP":
                    types = ReadTypeTable(reader, header, strings);
                    break;
                case "WKT":
                    wellKnownTypes = ReadWellKnownTypes(reader);
                    break;
                case "EOS":
                    // End of strings marker in some files.
                    break;
                case "EOH":
                    // End of definition header - the remainder of the file contains value changes.
                    ReadValueChanges(reader, header, definition, signalTypeById, types, wellKnownTypes,
                        declaredSnapshotValueCount, null);
                    return ghwFile;
                case "SNP":
                case "CYC":
                    ReadValueChanges(reader, header, definition, signalTypeById, types, wellKnownTypes,
                        declaredSnapshotValueCount, tag);
                    return ghwFile;
                case "DIR":
                case "TAI":
                case "EOD":
                    // These sections may appear before EOH in some writers; skip gracefully.
                    if (definition.SignalRegister.Count == 0 && stream.CanSeek)
                        TryReadHierarchyByTagScan(stream, header, definition, ref strings, ref types,
                            signalTypeById);
                    return ghwFile;
                default:
                    // Unknown tag - stop reading rather than risk misinterpreting data.
                    if (definition.SignalRegister.Count == 0 && stream.CanSeek)
                        TryReadHierarchyByTagScan(stream, header, definition, ref strings, ref types,
                            signalTypeById);
                    return ghwFile;
            }
        }

        if (definition.SignalRegister.Count == 0 && stream.CanSeek)
            TryReadHierarchyByTagScan(stream, header, definition, ref strings, ref types, signalTypeById);

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

        // Header layout:
        // [0..8]  = "GHDLwave\n"
        // [9]     = header info length (16)
        // [10]    = version major
        // [11]    = version minor
        // [12]    = endianness (1=little, 2=big)
        // [13]    = word length
        // [14]    = file offset length
        // [15]    = reserved zero byte
        var versionMajor = hdr[10];
        var versionMinor = hdr[11];
        var endianness = hdr[12];
        var wordLen = hdr[13];
        var offLen = hdr[14];

        if (endianness != 1 && endianness != 2)
            throw new InvalidDataException($"Invalid GHW endianness marker: {endianness}.");

        return new GhwHeader
        {
            VersionMajor = versionMajor,
            VersionMinor = versionMinor,
            BigEndian = endianness == 2,
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
        // Skip to the next known section tag.
        SkipUntilTag(reader, "WKT", "HIE", "EOH", "SNP", "CYC", "DIR", "TAI", "EOD");
        return types;
    }

    private static GhwWellKnownTypes ReadWellKnownTypes(GhwBinaryReader reader)
    {
        var result = new GhwWellKnownTypes();
        reader.Skip(4);

        while (true)
        {
            var kind = reader.ReadByte();
            if (kind == 0) break;

            var typeIdx = reader.ReadByte();
            switch (kind)
            {
                case 1:
                    result.BooleanType = typeIdx;
                    break;
                case 2:
                    result.BitType = typeIdx;
                    break;
                case 3:
                    result.StdUlogicType = typeIdx;
                    break;
            }
        }

        return result;
    }

    private static void ReadHierarchy(GhwBinaryReader reader, GhwHeader header, string[] strings, GhwType[] types,
        VcdDefinition definition, Dictionary<int, int> signalTypeById, ref int declaredSnapshotValueCount)
    {
        // Header: 4 reserved, int32 nbr_scopes, int32 nbr_scope_sigs, int32 nbr_sigs.
        reader.Skip(4);
        var nbrScopes = reader.ReadInt32(header.BigEndian);
        var nbrScopeSigs = reader.ReadInt32(header.BigEndian);
        var nbrSigs = reader.ReadInt32(header.BigEndian);

        if (nbrScopes < 0 || nbrScopes > 10_000_000)
            throw new InvalidDataException("GHW hierarchy scope count out of range.");
        if (nbrScopeSigs < 0 || nbrScopeSigs > 10_000_000)
            throw new InvalidDataException("GHW hierarchy scope signal count out of range.");
        if (nbrSigs < 0 || nbrSigs > 10_000_000)
            throw new InvalidDataException("GHW hierarchy signal count out of range.");

        // Snapshot sections store one value per declared signal.
        declaredSnapshotValueCount = Math.Max(declaredSnapshotValueCount, nbrSigs);

        IScopeHolder current = definition;
        var signalOrdinal = 0;
        while (true)
        {
            var kind = reader.ReadByte();

            switch (kind)
            {
                case 0: // End of declarations
                    return;
                case 1: // Design
                    // Design entry has a name; keep current at root.
                    reader.ReadByte();
                    break;
                case 15: // End-of-scope
                    if (current is VcdScope scope && scope.Parent != null) current = scope.Parent;
                    break;
                case 3: // Block
                case 4: // Generate-if
                case 5: // Generate-for
                case 6: // Instance
                case 7: // Package
                case 13: // Process
                case 14: // Generic
                {
                    var nameIdx = reader.ReadByte();
                    var name = LookupString(strings, nameIdx);
                    var newScope = new VcdScope(current, name);
                    current.Scopes.Add(newScope);
                    current = newScope;
                    break;
                }
                case 16: // Signal
                case 17: // Port-in
                case 18: // Port-out
                case 19: // Port-inout
                case 20: // Port-buffer
                case 21: // Port-linkage
                {
                    var nameIdx = reader.ReadByte();
                    var typeIdx = reader.ReadByte();
                    _ = reader.ReadByte(); // Encoded signal id; CYC/SNP keys are positional.
                    var name = LookupString(strings, nameIdx);
                    signalOrdinal++;

                    var id = signalOrdinal.ToString();
                    var bitWidth = (typeIdx >= 0 && typeIdx < types.Length) ? Math.Max(1, types[typeIdx].BitWidth) : 1;

                    IVcdSignal signal = bitWidth == 1
                        ? new VcdSignal<StdLogic>(definition.ChangeTimes, VcdLineType.Wire, bitWidth, id, name)
                        : new VcdSignal<StdLogic[]>(definition.ChangeTimes, VcdLineType.Wire, bitWidth, id, name);
                    current.Signals.Add(signal);
                    definition.SignalRegister[id] = signal;
                    signalTypeById[signalOrdinal] = typeIdx;
                    break;
                }
                default:
                    throw new InvalidDataException($"Unsupported GHW hierarchy declaration kind: {kind}.");
            }
        }
    }

    private static void ReadValueChanges(GhwBinaryReader reader, GhwHeader header, VcdDefinition definition,
        Dictionary<int, int> signalTypeById, GhwType[] types, GhwWellKnownTypes wellKnownTypes,
        int declaredSnapshotValueCount, string? firstTag)
    {
        long currentTime = 0;
        var snapshotValueCount = declaredSnapshotValueCount > 0 ? declaredSnapshotValueCount : definition.SignalRegister.Count;
        var tag = firstTag;

        while (true)
        {
            tag ??= reader.ReadTag();
            if (tag is null) break;

            switch (tag)
            {
                case "SNP":
                    ReadSnapshot(reader, header, definition, signalTypeById, types, wellKnownTypes, snapshotValueCount,
                        ref currentTime);
                    break;
                case "CYC":
                    ReadCycle(reader, header, definition, signalTypeById, types, wellKnownTypes, ref currentTime);
                    break;
                case "ECY":
                case "ESN":
                    // Section footers; continue scanning for subsequent sections.
                    break;
                case "DIR":
                case "TAI":
                case "EOD":
                    return;
                default:
                    return;
            }

            tag = null;
        }

        if (definition.ChangeTimes.Count == 0) definition.ChangeTimes.Add(0);
    }

    private static string LookupString(string[] strings, int index)
    {
        if (index >= 0 && index < strings.Length) return strings[index];
        return string.Empty;
    }

    private static void TryReadHierarchyByTagScan(Stream stream, GhwHeader header, VcdDefinition definition,
        ref string[] strings, ref GhwType[] types, Dictionary<int, int> signalTypeById)
    {
        if (TryFindTag(stream, "STR", out var strPos))
        {
            stream.Position = strPos + 4;
            strings = ReadStringTable(new GhwBinaryReader(stream), header);
        }

        if (TryFindTag(stream, "HIE", out var hiePos))
        {
            stream.Position = hiePos + 4;
            var declaredSignalCount = 0;
            ReadHierarchy(new GhwBinaryReader(stream), header, strings, types, definition, signalTypeById,
                ref declaredSignalCount);
        }

        if (definition.ChangeTimes.Count == 0) definition.ChangeTimes.Add(0);
    }

    private static bool TryFindTag(Stream stream, string tag, out long position)
    {
        position = -1;
        if (!stream.CanSeek) return false;

        var previous = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> expected = stackalloc byte[4];
            Encoding.ASCII.GetBytes(tag.PadRight(4, '\0')).CopyTo(expected);
            Span<byte> buffer = stackalloc byte[4];

            while (stream.Position <= stream.Length - 4)
            {
                var candidate = stream.Position;
                if (stream.Read(buffer) < 4) return false;
                if (buffer.SequenceEqual(expected))
                {
                    position = candidate;
                    return true;
                }

                stream.Position = candidate + 1;
            }

            return false;
        }
        finally
        {
            stream.Position = previous;
        }
    }

    private static void SkipUntilTag(GhwBinaryReader reader, params string[] expectedTags)
    {
        Span<byte> window = stackalloc byte[4];
        var filled = 0;
        var expected = new HashSet<string>(expectedTags);

        while (reader.TryReadByte(out var next))
        {
            if (filled < 4)
            {
                window[filled++] = next;
                if (filled < 4) continue;
            }
            else
            {
                window[0] = window[1];
                window[1] = window[2];
                window[2] = window[3];
                window[3] = next;
            }

            if (window[3] != 0) continue;

            var tag = Encoding.ASCII.GetString(window[..3]);
            if (expected.Contains(tag))
            {
                reader.Rewind(4);
                return;
            }
        }
    }

    private static void ParseByTagScan(Stream stream, GhwHeader header, VcdDefinition definition, ref string[] strings,
        ref GhwType[] types, Dictionary<int, int> signalTypeById, ref GhwWellKnownTypes wellKnownTypes,
        ref int declaredSnapshotValueCount)
    {
        if (TryFindTag(stream, "STR", out var strPos))
        {
            stream.Position = strPos + 4;
            strings = ReadStringTable(new GhwBinaryReader(stream), header);
        }

        if (TryFindTag(stream, "WKT", out var wktPos))
        {
            stream.Position = wktPos + 4;
            wellKnownTypes = ReadWellKnownTypes(new GhwBinaryReader(stream));
        }

        if (TryFindTag(stream, "HIE", out var hiePos))
        {
            stream.Position = hiePos + 4;
            ReadHierarchy(new GhwBinaryReader(stream), header, strings, types, definition, signalTypeById,
                ref declaredSnapshotValueCount);
        }

        if (TryFindTag(stream, "EOH", out var eohPos))
        {
            stream.Position = eohPos + 4;
            ReadValueChanges(new GhwBinaryReader(stream), header, definition, signalTypeById, types, wellKnownTypes,
                declaredSnapshotValueCount, null);
            return;
        }

        if (TryFindTag(stream, "SNP", out var snpPos))
        {
            stream.Position = snpPos + 4;
            ReadValueChanges(new GhwBinaryReader(stream), header, definition, signalTypeById, types, wellKnownTypes,
                declaredSnapshotValueCount, "SNP");
            return;
        }

        if (TryFindTag(stream, "CYC", out var cycPos))
        {
            stream.Position = cycPos + 4;
            ReadValueChanges(new GhwBinaryReader(stream), header, definition, signalTypeById, types, wellKnownTypes,
                declaredSnapshotValueCount, "CYC");
            return;
        }

        if (definition.ChangeTimes.Count == 0) definition.ChangeTimes.Add(0);
    }

    private static void ReadSnapshot(GhwBinaryReader reader, GhwHeader header, VcdDefinition definition,
        Dictionary<int, int> signalTypeById, GhwType[] types, GhwWellKnownTypes wellKnownTypes, int snapshotValueCount,
        ref long currentTime)
    {
        reader.Skip(4);
        currentTime = (long)reader.ReadUInt64(header.BigEndian);
        var timeIndex = AddChangeTime(definition, currentTime);

        for (var i = 0; i < snapshotValueCount; i++)
        {
            var rawValue = reader.ReadByte();
            var signalId = i + 1;
            ApplySignalChange(definition, signalTypeById, types, wellKnownTypes, signalId, rawValue, timeIndex);
        }

        // Optional section terminator emitted by some writers.
        if (reader.PeekTag("ESN")) reader.ReadTag();
    }

    private static void ReadCycle(GhwBinaryReader reader, GhwHeader header, VcdDefinition definition,
        Dictionary<int, int> signalTypeById, GhwType[] types, GhwWellKnownTypes wellKnownTypes, ref long currentTime)
    {
        currentTime = (long)reader.ReadUInt64(header.BigEndian);
        var baseTimeIndex = AddChangeTime(definition, currentTime);
        ReadSignalChanges(reader, definition, signalTypeById, types, wellKnownTypes, baseTimeIndex);

        while (true)
        {
            var dt = ReadVarUInt(reader);
            if (dt == 127) break;

            currentTime += (long)dt;
            var timeIndex = AddChangeTime(definition, currentTime);
            ReadSignalChanges(reader, definition, signalTypeById, types, wellKnownTypes, timeIndex);
        }

        if (reader.PeekTag("ECY")) reader.ReadTag();
    }

    private static ulong ReadVarUInt(GhwBinaryReader reader)
    {
        ulong value = 0;
        var shift = 0;
        while (true)
        {
            var b = reader.ReadByte();
            value |= ((ulong)(b & 0x7f)) << shift;
            if ((b & 0x80) == 0) return value;
            shift += 7;
            if (shift > 56) throw new InvalidDataException("GHW delta-time varint is too large.");
        }
    }

    private static int AddChangeTime(VcdDefinition definition, long time)
    {
        if (definition.ChangeTimes.Count == 0 || definition.ChangeTimes[^1] != time)
            definition.ChangeTimes.Add(time);
        return definition.ChangeTimes.Count - 1;
    }

    private static void ApplySignalChange(VcdDefinition definition, Dictionary<int, int> signalTypeById, GhwType[] types,
        GhwWellKnownTypes wellKnownTypes, int signalId, byte rawValue, int timeIndex)
    {
        if (!definition.SignalRegister.TryGetValue(signalId.ToString(), out var signal)) return;

        signalTypeById.TryGetValue(signalId, out var typeIdx);
        var type = (typeIdx >= 0 && typeIdx < types.Length) ? types[typeIdx] : null;
        var logic = DecodeStdLogic(rawValue, type, wellKnownTypes, typeIdx);

        if (signal is VcdSignal<StdLogic> scalarSignal)
        {
            scalarSignal.AddChange(timeIndex, logic);
            return;
        }

        if (signal is VcdSignal<StdLogic[]> vectorSignal)
        {
            var bitWidth = Math.Max(1, vectorSignal.BitWidth);
            var value = Enumerable.Repeat(logic, bitWidth).ToArray();
            vectorSignal.AddChange(timeIndex, value);
        }
    }

    private static StdLogic DecodeStdLogic(byte rawValue, GhwType? type, GhwWellKnownTypes wellKnownTypes, int typeIdx)
    {
        if (typeIdx == wellKnownTypes.BitType || typeIdx == wellKnownTypes.BooleanType || type?.Kind == GhwTypeKind.B2)
            return rawValue == 0 ? StdLogic.Zero : StdLogic.Full;

        if (typeIdx == wellKnownTypes.StdUlogicType || type?.Kind == GhwTypeKind.E8)
            return rawValue switch
            {
                0 => StdLogic.U,
                1 => StdLogic.X,
                2 => StdLogic.Zero,
                3 => StdLogic.Full,
                4 => StdLogic.Z,
                5 => StdLogic.W,
                6 => StdLogic.L,
                7 => StdLogic.H,
                8 => StdLogic.DontCare,
                _ => StdLogic.X
            };

        return rawValue == 0 ? StdLogic.Zero : StdLogic.Full;
    }

    private static void ReadSignalChanges(GhwBinaryReader reader, VcdDefinition definition,
        Dictionary<int, int> signalTypeById, GhwType[] types, GhwWellKnownTypes wellKnownTypes, int timeIndex)
    {
        while (true)
        {
            var signalId = reader.ReadByte();
            if (signalId == 0) break;
            var rawValue = ReadSignalValueByte(reader, signalTypeById, types, wellKnownTypes, signalId, true);
            ApplySignalChange(definition, signalTypeById, types, wellKnownTypes, signalId, rawValue, timeIndex);
        }
    }

    private static byte ReadSignalValueByte(GhwBinaryReader reader, Dictionary<int, int> signalTypeById, GhwType[] types,
        GhwWellKnownTypes wellKnownTypes, int signalId, bool compressed)
    {
        signalTypeById.TryGetValue(signalId, out var typeIdx);
        var type = (typeIdx >= 0 && typeIdx < types.Length) ? types[typeIdx] : null;

        var isSingleByteLogicType = typeIdx == wellKnownTypes.BitType || typeIdx == wellKnownTypes.BooleanType ||
            typeIdx == wellKnownTypes.StdUlogicType || type?.Kind is GhwTypeKind.B2 or GhwTypeKind.E8;

        if (isSingleByteLogicType || !compressed) return reader.ReadByte();

        // Common integer subtype in observed GHW files.
        if (typeIdx == 4)
        {
            reader.Skip(3);
            return reader.ReadByte();
        }

        var value = ReadVarUInt(reader);
        return (byte)(value & 0xff);
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

    public bool TryReadByte(out byte value)
    {
        var b = _stream.ReadByte();
        if (b < 0)
        {
            value = 0;
            return false;
        }

        value = (byte)b;
        return true;
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

    public ulong ReadUInt64(bool bigEndian)
    {
        Span<byte> b = stackalloc byte[8];
        ReadExact(b);
        return bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(b) : BinaryPrimitives.ReadUInt64LittleEndian(b);
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

    public void Rewind(int bytes)
    {
        if (!_stream.CanSeek)
            throw new NotSupportedException("Cannot rewind a non-seekable stream.");
        _stream.Seek(-bytes, SeekOrigin.Current);
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

    public bool PeekTag(string tag3)
    {
        if (!_stream.CanSeek) return false;

        var current = _stream.Position;
        try
        {
            Span<byte> b = stackalloc byte[4];
            if (_stream.Read(b) < 4) return false;
            return b[0] == tag3[0] && b[1] == tag3[1] && b[2] == tag3[2] && b[3] == 0;
        }
        finally
        {
            _stream.Position = current;
        }
    }
}

internal sealed class GhwType
{
    public int BitWidth { get; init; }
    public GhwTypeKind Kind { get; init; }
}

internal enum GhwTypeKind : byte
{
    Unknown = 0,
    B2 = 25,
    E8 = 23
}

internal struct GhwWellKnownTypes
{
    public byte BooleanType { get; set; }
    public byte BitType { get; set; }
    public byte StdUlogicType { get; set; }
}
