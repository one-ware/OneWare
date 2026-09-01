namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// How the memory panel reads and presents one target's memory. Every member has a byte-addressed
/// default, so a target that states nothing keeps the behaviour it has today.
/// <para>
/// This exists because memory geometry is not something a generic panel can derive. On a machine
/// whose smallest addressable unit is wider than a byte — a soft core on an FPGA, a DSP — the
/// debug information reports addresses in units while the backend reads bytes, and the panel is
/// off by that factor with nothing in it that could know the factor. Passing it as data keeps the
/// panel free of any one target's arithmetic.
/// </para>
/// </summary>
public sealed record DebugMemoryProfile
{
    /// <summary>
    /// A byte-addressed target that states nothing beyond that. Used whenever a request carries
    /// no profile of its own.
    /// </summary>
    public static DebugMemoryProfile Default { get; } = new();

    /// <summary>
    /// Bytes per addressable unit of the target: <c>1</c> for a byte-addressed machine, <c>2</c>
    /// for one whose smallest addressable unit is a 16-bit word. The panel scales both the
    /// address and the length by this before asking the backend, and groups the bytes it gets
    /// back into units of this width, least significant byte first.
    /// </summary>
    public int AddressableUnitBytes { get; init; } = 1;

    /// <summary>
    /// Length a newly added watch starts with, in addressable units.
    /// </summary>
    public int DefaultLength { get; init; } = 4;

    /// <summary>
    /// Width of one target word in bits, used when a raw value is re-based or read as a signed
    /// number. Distinct from <see cref="AddressableUnitBytes"/>: a byte-addressed 32-bit machine
    /// states <c>1</c> there and <c>32</c> here. Left unset it follows the addressable unit, which
    /// is what a panel would otherwise have had to assume.
    /// </summary>
    public int WordBits
    {
        get => _wordBits ?? AddressableUnitBytes * 8;
        init => _wordBits = value;
    }

    private readonly int? _wordBits;

    /// <summary>
    /// Byte order within one addressable unit. <see langword="true"/> — the default — means the
    /// least significant byte comes first, which is how the backend hands the bytes over for
    /// nearly every target. A big-endian target that says nothing here would have every word
    /// displayed byte-swapped, and nothing in the panel could notice.
    /// </summary>
    public bool IsLittleEndian { get; init; } = true;

    /// <summary>
    /// Example shown in the empty address box. A target whose addresses look nothing like the
    /// generic example is markedly easier to use with one of its own.
    /// </summary>
    public string AddressWatermark { get; init; } = "Address, e.g. 0x0 or &buffer";
}
