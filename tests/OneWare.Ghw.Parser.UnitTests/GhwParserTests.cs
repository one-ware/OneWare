using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using OneWare.Ghw.Parser;
using Xunit;

namespace OneWare.Ghw.Parser.UnitTests;

public class GhwParserTests
{
    /// <summary>
    ///     Builds a minimal in-memory GHW stream containing a header, a string table and a small
    ///     hierarchy with one scope and two signals. The produced stream exercises the structural
    ///     parsing path without relying on any external asset files.
    /// </summary>
    private static MemoryStream BuildSyntheticGhw()
    {
        var ms = new MemoryStream();

        // 16-byte header + 1 byte offset length.
        // "GHDLwave\n" magic, marker bytes (16,0,0), version 1.1, endianness 2 (little endian), word=4, off=4
        var magic = Encoding.ASCII.GetBytes("GHDLwave\n");
        ms.Write(magic, 0, magic.Length);
        ms.WriteByte(16); // marker
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(1); // version major
        ms.WriteByte(1); // version minor
        ms.WriteByte(2); // little endian
        ms.WriteByte(4); // word length
        ms.WriteByte(4); // offset length

        // STR section: tag + 4 reserved + int32 count + int32 totalSize + payload
        WriteTag(ms, "STR");
        WriteInt32Le(ms, 0);
        // strings: "top", "clk", "data"  (indexes 1, 2, 3)
        var stringsPayload = Encoding.ASCII.GetBytes("top\0clk\0data\0");
        WriteInt32Le(ms, 3); // count
        WriteInt32Le(ms, stringsPayload.Length);
        ms.Write(stringsPayload, 0, stringsPayload.Length);

        // HIE section: tag + 4 reserved + int32 nbrScopes + int32 nbrSigs + int32 nbrDecls + entries
        WriteTag(ms, "HIE");
        WriteInt32Le(ms, 0);
        WriteInt32Le(ms, 1); // nbrScopes
        WriteInt32Le(ms, 2); // nbrSigs
        WriteInt32Le(ms, 5); // nbrDecls (scope + 2 signals + end-scope + end)
        // Entry 1: block (kind=3), name index 1 ("top")
        ms.WriteByte(3);
        WriteInt32Le(ms, 1);
        // Entry 2: signal (kind=9), name index 2 ("clk"), type index 0
        ms.WriteByte(9);
        WriteInt32Le(ms, 2);
        WriteInt32Le(ms, 0);
        // Entry 3: signal (kind=9), name index 3 ("data"), type index 0
        ms.WriteByte(9);
        WriteInt32Le(ms, 3);
        WriteInt32Le(ms, 0);
        // Entry 4: end-of-scope
        ms.WriteByte(15);
        // Entry 5: end marker
        ms.WriteByte(0);

        // EOH terminates the definition and ends our synthetic file.
        WriteTag(ms, "EOH");

        ms.Position = 0;
        return ms;
    }

    private static void WriteTag(MemoryStream ms, string tag)
    {
        var bytes = Encoding.ASCII.GetBytes(tag.PadRight(4, '\0'));
        ms.Write(bytes, 0, 4);
    }

    private static void WriteInt32Le(MemoryStream ms, int value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, value);
        ms.Write(b);
    }

    [Fact]
    public void ParseHeaderDetectsMagic()
    {
        using var ms = BuildSyntheticGhw();
        var file = GhwParser.ParseGhw(ms);
        Assert.Equal(1, file.VersionMajor);
        Assert.Equal(1, file.VersionMinor);
        Assert.True(file.IsLittleEndian);
        Assert.Equal(4, file.WordLength);
        Assert.Equal(4, file.OffsetLength);
    }

    [Fact]
    public void ParseBuildsHierarchy()
    {
        using var ms = BuildSyntheticGhw();
        var file = GhwParser.ParseGhw(ms);
        var definition = file.VcdFile.Definition;

        Assert.Single(definition.Scopes);
        var top = definition.Scopes[0];
        Assert.Equal("top", top.Name);
        Assert.Equal(2, top.Signals.Count);
        Assert.Equal("clk", top.Signals[0].Name);
        Assert.Equal("data", top.Signals[1].Name);
        Assert.Equal(2, definition.SignalRegister.Count);
    }

    [Fact]
    public void ParseRejectsInvalidMagic()
    {
        var bytes = new byte[32];
        Encoding.ASCII.GetBytes("NotAGhwFile\n").CopyTo(bytes, 0);
        using var ms = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => GhwParser.ParseGhw(ms));
    }
}
