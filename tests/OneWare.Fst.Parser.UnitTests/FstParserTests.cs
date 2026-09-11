using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using OneWare.Fst.Parser;
using Xunit;

namespace OneWare.Fst.Parser.UnitTests;

public class FstParserTests
{
    /// <summary>
    ///     Builds a minimal in-memory FST stream with a header block and a gzip-compressed hierarchy
    ///     block containing one scope and two signals. The produced stream exercises the structural
    ///     parsing path without relying on any external asset files.
    /// </summary>
    private static MemoryStream BuildSyntheticFst()
    {
        var ms = new MemoryStream();

        // --- Header block (type 0) ---------------------------------------------------------------
        var headerPayload = new byte[329];
        // start_time = 0, end_time = 1000, endian_test = ~2.71828 (skipped), mem_used = 0,
        // num_scopes = 1, num_vars = 2, max_handle = 2, num_vc_blocks = 0, timescale = -9 (ns)
        WriteU64Be(headerPayload, 0, 0);
        WriteU64Be(headerPayload, 8, 1000);
        // Write a reasonable double for the endian-test field so it doesn't trip a checker.
        BitConverter.GetBytes(Math.E).CopyTo(headerPayload, 16);
        WriteU64Be(headerPayload, 24, 0);
        WriteU64Be(headerPayload, 32, 1);
        WriteU64Be(headerPayload, 40, 2);
        WriteU64Be(headerPayload, 48, 2);
        WriteU64Be(headerPayload, 56, 0);
        headerPayload[64] = unchecked((byte)-9);

        WriteBlock(ms, 0, headerPayload);

        // --- Hierarchy block (type 4): uclen (u64 BE) + gzip stream -------------------------------
        var hier = new MemoryStream();
        // Scope: tag 254, scope type 0, "top\0", component "\0"
        hier.WriteByte(254);
        hier.WriteByte(0);
        WriteCString(hier, "top");
        WriteCString(hier, "");
        // Variable: tag 3 (e.g. wire), direction 0, name "clk\0", length 1 (var-int), alias 0
        hier.WriteByte(3);
        hier.WriteByte(0);
        WriteCString(hier, "clk");
        WriteVarInt(hier, 1);
        WriteVarInt(hier, 0);
        // Variable: tag 3, direction 0, name "data", length 8, alias 0
        hier.WriteByte(3);
        hier.WriteByte(0);
        WriteCString(hier, "data");
        WriteVarInt(hier, 8);
        WriteVarInt(hier, 0);
        // Upscope: tag 255
        hier.WriteByte(255);

        var uncompressed = hier.ToArray();

        // gzip-compress the hierarchy payload
        using var compressedStream = new MemoryStream();
        using (var gz = new GZipStream(compressedStream, CompressionLevel.Fastest, true))
        {
            gz.Write(uncompressed, 0, uncompressed.Length);
        }

        var compressed = compressedStream.ToArray();

        // Build the block payload: 8 bytes uclen + gzip data
        var payload = new byte[8 + compressed.Length];
        WriteU64Be(payload, 0, (ulong)uncompressed.Length);
        Array.Copy(compressed, 0, payload, 8, compressed.Length);
        WriteBlock(ms, 4, payload);

        ms.Position = 0;
        return ms;
    }

    private static void WriteBlock(MemoryStream ms, byte blockType, byte[] payload)
    {
        ms.WriteByte(blockType);
        Span<byte> lenBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(lenBytes, (ulong)(8 + payload.Length));
        ms.Write(lenBytes);
        ms.Write(payload, 0, payload.Length);
    }

    private static void WriteU64Be(byte[] buffer, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(offset), value);
    }

    private static void WriteCString(MemoryStream ms, string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        ms.Write(bytes, 0, bytes.Length);
        ms.WriteByte(0);
    }

    private static void WriteVarInt(MemoryStream ms, ulong value)
    {
        while (value >= 0x80)
        {
            ms.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        ms.WriteByte((byte)value);
    }

    [Fact]
    public void ParseReadsHeaderFields()
    {
        using var ms = BuildSyntheticFst();
        var file = FstParser.ParseFst(ms);
        Assert.Equal(0, file.StartTime);
        Assert.Equal(1000, file.EndTime);
        Assert.Equal(-9, file.TimeScaleExponent);
        Assert.Equal(1, file.NumScopes);
        Assert.Equal(2, file.NumVars);
    }

    [Fact]
    public void ParseBuildsHierarchy()
    {
        using var ms = BuildSyntheticFst();
        var file = FstParser.ParseFst(ms);
        var definition = file.VcdFile.Definition;

        Assert.Single(definition.Scopes);
        var top = definition.Scopes[0];
        Assert.Equal("top", top.Name);
        Assert.Equal(2, top.Signals.Count);
        Assert.Equal("clk", top.Signals[0].Name);
        Assert.Equal(1, top.Signals[0].BitWidth);
        Assert.Equal("data", top.Signals[1].Name);
        Assert.Equal(8, top.Signals[1].BitWidth);
    }

    [Fact]
    public void ParseRejectsMissingHeader()
    {
        using var ms = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => FstParser.ParseFst(ms));
    }
}
