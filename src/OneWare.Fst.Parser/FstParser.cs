using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OneWare.Fst.Parser.Data;
using OneWare.Vcd.Parser.Data;

namespace OneWare.Fst.Parser;

/// <summary>
///     Parser for the FST (Fast Signal Trace) binary waveform format produced by GTKWave / libfst.
///     The API mirrors <see cref="OneWare.Vcd.Parser.VcdParser" /> so the file can be consumed by the existing wave form
///     viewer.
/// </summary>
/// <remarks>
///     <para>
///         This parser reads the FST header block and the (gzip-compressed) hierarchy block so that
///         scopes and signals can be displayed by the viewer. Value change blocks use a highly packed
///         encoding with custom variable-length integers and per-block compression; decoding them is
///         deliberately left to a future iteration and unknown block types are simply skipped.
///     </para>
///     <para>
///         Reference: libfst (<c>fstapi.c</c>) from the GTKWave project. The FST file layout is a
///         sequence of blocks, each starting with a one byte type and an eight byte big-endian length
///         that includes the length field itself.
///     </para>
/// </remarks>
public static class FstParser
{
    private const byte BlockHeader = 0;
    private const byte BlockValueChange = 1;
    private const byte BlockBlackout = 2;
    private const byte BlockGeometry = 3;
    private const byte BlockHierarchy = 4;
    private const byte BlockValueChangeDynAlias = 5;
    private const byte BlockHierarchyLz4 = 6;
    private const byte BlockHierarchyLz4Duo = 7;
    private const byte BlockValueChangeDynAlias2 = 8;
    private const byte BlockZWrapper = 254;
    private const byte BlockSkip = 255;

    // Hierarchy entry tags as emitted by libfst.
    private const byte HierScope = 254;
    private const byte HierUpScope = 255;
    private const byte HierAttrBegin = 252;
    private const byte HierAttrEnd = 253;

    public static Task<FstFile> ParseFstAsync(string path)
    {
        return Task.Run(() => ParseFst(path));
    }

    public static FstFile ParseFst(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
        return ParseFst(stream);
    }

    public static FstFile ParseFst(Stream stream)
    {
        var definition = new VcdDefinition();
        var vcdFile = new VcdFile(definition);

        long startTime = 0;
        long endTime = 0;
        var timeScaleExp = 0;
        long numScopes = 0;
        long numVars = 0;
        var headerRead = false;
        var hierarchyRead = false;

        Span<byte> lenBytes = stackalloc byte[8];

        while (true)
        {
            var blockType = stream.ReadByte();
            if (blockType < 0) break;

            if (!ReadExact(stream, lenBytes)) break;
            var blockLen = (long)BinaryPrimitives.ReadUInt64BigEndian(lenBytes);
            if (blockLen < 8) throw new InvalidDataException($"FST block length {blockLen} is invalid.");

            var payloadLen = blockLen - 8;
            var payloadStart = stream.Position;

            switch (blockType)
            {
                case BlockHeader:
                    ReadHeaderBlock(stream, out startTime, out endTime, out timeScaleExp, out numScopes, out numVars);
                    headerRead = true;
                    break;
                case BlockHierarchy:
                    ReadHierarchyGzip(stream, payloadLen, definition);
                    hierarchyRead = true;
                    break;
                case BlockHierarchyLz4:
                case BlockHierarchyLz4Duo:
                    // LZ4 decompression is not available in the standard library; skip these blocks
                    // so that files without LZ4 still open successfully.
                    break;
                case BlockGeometry:
                case BlockBlackout:
                case BlockValueChange:
                case BlockValueChangeDynAlias:
                case BlockValueChangeDynAlias2:
                case BlockZWrapper:
                case BlockSkip:
                default:
                    // Skip unimplemented / unknown block types.
                    break;
            }

            // Always advance to the end of the block, whether or not we consumed its payload.
            var consumed = stream.Position - payloadStart;
            if (consumed < payloadLen) stream.Seek(payloadLen - consumed, SeekOrigin.Current);
            else if (consumed > payloadLen)
                throw new InvalidDataException("FST block overran its declared length.");

            if (headerRead && hierarchyRead)
            {
                // Nothing else we currently act on; stop scanning.
                break;
            }
        }

        if (!headerRead) throw new InvalidDataException("FST file is missing a header block.");

        // Seed the time axis with at least one entry so the viewer has a valid range to render.
        if (definition.ChangeTimes.Count == 0) definition.ChangeTimes.Add(startTime);

        // Map the FST power-of-ten time scale to the VCD time scale which is expressed in femtoseconds.
        definition.TimeScale = ConvertTimeScale(timeScaleExp);

        return new FstFile(vcdFile)
        {
            StartTime = startTime,
            EndTime = endTime,
            TimeScaleExponent = timeScaleExp,
            NumScopes = numScopes,
            NumVars = numVars
        };
    }

    private static void ReadHeaderBlock(Stream stream, out long startTime, out long endTime,
        out int timeScaleExp, out long numScopes, out long numVars)
    {
        // The header block payload is a fixed 320+ byte structure. We read only the fields we need.
        Span<byte> buf = stackalloc byte[8];

        ReadExactOrThrow(stream, buf);
        startTime = (long)BinaryPrimitives.ReadUInt64BigEndian(buf);

        ReadExactOrThrow(stream, buf);
        endTime = (long)BinaryPrimitives.ReadUInt64BigEndian(buf);

        // Skip endian double (8), mem_used (8).
        stream.Seek(16, SeekOrigin.Current);

        ReadExactOrThrow(stream, buf);
        numScopes = (long)BinaryPrimitives.ReadUInt64BigEndian(buf);

        ReadExactOrThrow(stream, buf);
        numVars = (long)BinaryPrimitives.ReadUInt64BigEndian(buf);

        // Skip max vc handle (8), vc block count (8).
        stream.Seek(16, SeekOrigin.Current);

        var scale = stream.ReadByte();
        if (scale < 0) throw new EndOfStreamException();
        // Time scale is a signed exponent (e.g. -9 for ns).
        timeScaleExp = (sbyte)scale;

        // The remaining payload contains version/date/etc. fields that the outer loop will skip.
    }

    private static void ReadHierarchyGzip(Stream stream, long payloadLen, VcdDefinition definition)
    {
        // The hierarchy block payload starts with an 8-byte big-endian uncompressed length,
        // followed by a gzip stream.
        if (payloadLen < 8) return;

        Span<byte> ucLenBytes = stackalloc byte[8];
        ReadExactOrThrow(stream, ucLenBytes);
        var uclen = (long)BinaryPrimitives.ReadUInt64BigEndian(ucLenBytes);
        if (uclen < 0 || uclen > int.MaxValue)
            throw new InvalidDataException($"FST hierarchy uncompressed length {uclen} is invalid.");

        var compressedLen = payloadLen - 8;
        if (compressedLen <= 0) return;

        var compressed = new byte[compressedLen];
        ReadExactOrThrow(stream, compressed);

        using var compressedStream = new MemoryStream(compressed, false);
        using var gz = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream((int)uclen);
        gz.CopyTo(decompressed);
        decompressed.Position = 0;

        ParseHierarchyStream(decompressed, definition);
    }

    private static void ParseHierarchyStream(Stream data, VcdDefinition definition)
    {
        IScopeHolder current = definition;
        var signalIndex = 1;

        while (data.Position < data.Length)
        {
            var tag = data.ReadByte();
            if (tag < 0) break;

            switch (tag)
            {
                case HierScope:
                {
                    data.ReadByte(); // scope type
                    var name = ReadNullTerminated(data);
                    ReadNullTerminated(data); // component name
                    var newScope = new VcdScope(current, name);
                    current.Scopes.Add(newScope);
                    current = newScope;
                    break;
                }
                case HierUpScope:
                    if (current is VcdScope s && s.Parent != null) current = s.Parent;
                    break;
                case HierAttrBegin:
                {
                    _ = data.ReadByte(); // attr type
                    _ = data.ReadByte(); // sub type
                    _ = ReadNullTerminated(data); // name
                    _ = ReadVarInt(data); // arg
                    break;
                }
                case HierAttrEnd:
                    break;
                default:
                {
                    // Variable entry: tag is the var type (0..29).
                    var direction = data.ReadByte();
                    if (direction < 0) return;
                    var name = ReadNullTerminated(data);
                    var length = (int)ReadVarInt(data);
                    // Alias handle - a value of 0 signals a fresh signal; non-zero aliases share
                    // storage with a previous variable. We currently model both as independent
                    // signals for display purposes.
                    ReadVarInt(data);

                    if (length <= 0) length = 1;

                    var id = signalIndex.ToString();
                    signalIndex++;

                    var lineType = tag switch
                    {
                        6 => VcdLineType.Integer,
                        8 => VcdLineType.Real,
                        19 => VcdLineType.Event,
                        _ => VcdLineType.Wire
                    };

                    var signal = new VcdSignal<StdLogic[]>(definition.ChangeTimes, lineType, length, id, name);
                    current.Signals.Add(signal);
                    definition.SignalRegister[id] = signal;
                    break;
                }
            }
        }
    }

    private static string ReadNullTerminated(Stream data)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var b = data.ReadByte();
            if (b <= 0) break;
            sb.Append((char)b);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Reads an unsigned LEB128 variable-length integer as used inside FST hierarchy streams.
    /// </summary>
    private static ulong ReadVarInt(Stream data)
    {
        ulong result = 0;
        var shift = 0;
        while (shift < 64)
        {
            var b = data.ReadByte();
            if (b < 0) break;
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }

        return result;
    }

    private static long ConvertTimeScale(int exponent)
    {
        // VCD parser expresses the time scale in femtoseconds. Convert 10^exponent seconds to fs:
        // 10^(exponent + 15). Clamp the result to keep the number manageable for extreme values.
        var fsExp = exponent + 15;
        if (fsExp < 0) return 1;
        if (fsExp > 18) return 1_000_000_000_000_000_000; // cap at 10^18 fs = 10^3 s
        long scale = 1;
        for (var i = 0; i < fsExp; i++) scale *= 10;
        return scale;
    }

    private static bool ReadExact(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer.Slice(total));
            if (read <= 0) return false;
            total += read;
        }

        return true;
    }

    private static void ReadExactOrThrow(Stream stream, Span<byte> buffer)
    {
        if (!ReadExact(stream, buffer)) throw new EndOfStreamException();
    }
}
