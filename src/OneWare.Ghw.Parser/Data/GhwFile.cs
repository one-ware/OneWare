using OneWare.Vcd.Parser.Data;

namespace OneWare.Ghw.Parser.Data;

/// <summary>
///     Represents a parsed GHW (GHDL Wave) file.
///     Wraps a <see cref="VcdFile" /> so the data can be consumed by the existing wave form viewer.
/// </summary>
public class GhwFile
{
    public GhwFile(VcdFile vcdFile)
    {
        VcdFile = vcdFile;
    }

    /// <summary>
    ///     The underlying <see cref="VcdFile" /> populated from the GHW file.
    /// </summary>
    public VcdFile VcdFile { get; }

    /// <summary>
    ///     GHW format major version.
    /// </summary>
    public int VersionMajor { get; init; }

    /// <summary>
    ///     GHW format minor version.
    /// </summary>
    public int VersionMinor { get; init; }

    /// <summary>
    ///     True if the file is encoded in little endian, false for big endian.
    /// </summary>
    public bool IsLittleEndian { get; init; }

    /// <summary>
    ///     Word length (in bytes) used by the file, usually 4 or 8.
    /// </summary>
    public int WordLength { get; init; }

    /// <summary>
    ///     Offset length (in bytes) used by the file, usually 4 or 8.
    /// </summary>
    public int OffsetLength { get; init; }
}
