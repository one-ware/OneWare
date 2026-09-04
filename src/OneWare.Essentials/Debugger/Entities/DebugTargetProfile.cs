namespace OneWare.Essentials.Debugger.Entities;

/// <summary>
/// What the debugger needs to know about one target beyond its executable. Every member has a
/// byte-addressed default, so a target that states nothing keeps the behaviour it has today.
/// <para>
/// This exists because memory geometry is not something a generic panel can derive. On a machine
/// whose smallest addressable unit is wider than a byte — a soft core on an FPGA, a DSP — the
/// debug information reports addresses in units while the backend reads bytes, and the panel is
/// off by that factor with nothing in it that could know the factor. Passing it as data keeps the
/// panel free of any one target's arithmetic.
/// </para>
/// </summary>
public sealed record DebugTargetProfile
{
    /// <summary>
    /// A byte-addressed target that states nothing beyond that. Used whenever a request carries
    /// no profile of its own.
    /// </summary>
    public static DebugTargetProfile Default { get; } = new();

    /// <summary>
    /// Bytes per addressable unit of the target: <c>1</c> for a byte-addressed machine, <c>2</c>
    /// for one whose smallest addressable unit is a 16-bit word. The panel scales both the
    /// address and the length by this before asking the backend, and groups the bytes it gets
    /// back into units of this width, least significant byte first.
    /// </summary>
    public int AddressableUnitBytes { get; init; } = 1;

    /// <summary>
    /// Byte order within one addressable unit. <see langword="true"/> — the default — means the
    /// least significant byte comes first, which is how the backend hands the bytes over for
    /// nearly every target. A big-endian target that says nothing here would have every word
    /// displayed byte-swapped, and nothing in the panel could notice.
    /// </summary>
    public bool IsLittleEndian { get; init; } = true;

    /// <summary>
    /// Names of the registers that belong to this target, in the order they should be shown;
    /// <see langword="null"/> means every register the backend reports. A description written
    /// against a stock architecture carries that architecture's whole register file, of which
    /// only some registers exist in the hardware — without this list the panel shows the rest
    /// as well, and nothing in it could tell them apart.
    /// </summary>
    public IReadOnlyList<string>? Registers { get; init; }

    /// <summary>
    /// How many breakpoints the target can hold at once; <see langword="null"/> means no limit.
    /// A target that has run out can only refuse the next request, and the refusal alone does
    /// not say why — stating the number here lets whoever asks name the reason instead.
    /// </summary>
    public int? MaxBreakpoints { get; init; }

    /// <summary>
    /// Whether the target keeps a call stack. <see langword="false"/> for a target without
    /// subroutine calls, where stepping out of the current frame means nothing. Without this,
    /// the request is forwarded, the backend refuses it, and that refusal is the first hint the
    /// user gets that the button never applied — stating it here lets the button say so before
    /// it is pressed.
    /// </summary>
    public bool HasCallStack { get; init; } = true;

    /// <summary>
    /// Example shown in the empty address box. A target whose addresses look nothing like the
    /// generic example is markedly easier to use with one of its own.
    /// </summary>
    public string AddressWatermark { get; init; } = "Address, e.g. 0x0 or &buffer";
}
