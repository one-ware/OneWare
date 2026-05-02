using OneWare.Vcd.Parser.Data;

namespace OneWare.Fst.Parser.Data;

/// <summary>
///     Represents a parsed FST (Fast Signal Trace) file.
///     Wraps a <see cref="VcdFile" /> so the data can be consumed by the existing wave form viewer.
/// </summary>
public class FstFile
{
    public FstFile(VcdFile vcdFile)
    {
        VcdFile = vcdFile;
    }

    /// <summary>
    ///     The underlying <see cref="VcdFile" /> populated from the FST file.
    /// </summary>
    public VcdFile VcdFile { get; }

    /// <summary>
    ///     Start time of the trace, in the file's native time unit.
    /// </summary>
    public long StartTime { get; init; }

    /// <summary>
    ///     End time of the trace, in the file's native time unit.
    /// </summary>
    public long EndTime { get; init; }

    /// <summary>
    ///     Time scale of the file, expressed as a power of ten (e.g. -9 for nanoseconds).
    /// </summary>
    public int TimeScaleExponent { get; init; }

    /// <summary>
    ///     Total number of value change scopes recorded in the header.
    /// </summary>
    public long NumScopes { get; init; }

    /// <summary>
    ///     Total number of variables (signals) recorded in the header.
    /// </summary>
    public long NumVars { get; init; }
}
