namespace OneWare.Essentials.ProjectActions;

public enum BuildArtifactKind
{
    /// <summary>A program that runs on the host machine.</summary>
    Executable,

    /// <summary>An ELF file with symbols, e.g. firmware for a microcontroller or softcore.</summary>
    Elf,

    /// <summary>An Intel HEX image.</summary>
    Hex,

    /// <summary>A raw binary image.</summary>
    Binary,

    /// <summary>An FPGA bitstream.</summary>
    Bitstream,

    Other
}

/// <summary>
/// A file produced by a build. Run, Program and Debug use these instead of searching the project folder.
/// </summary>
/// <param name="Kind">What the file is.</param>
/// <param name="Path">Absolute path of the file.</param>
/// <param name="Description">Optional text for the UI, e.g. the configuration it was built with.</param>
public sealed record BuildArtifact(BuildArtifactKind Kind, string Path, string? Description = null);
