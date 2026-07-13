using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaToolchain
{
    public string Id { get; }

    public string Name { get; }

    /// <summary>
    /// Per-pin properties this toolchain wants to expose and persist in the pin planner.
    /// Override in toolchain implementations to declare properties such as IO Voltage.
    /// </summary>
    public IEnumerable<PinPropertyDefinition> PinProperties => [];

    public void OnProjectCreated(UniversalFpgaProjectRoot project);

    public void LoadConnections(UniversalFpgaProjectRoot project, FpgaModel fpga);

    public void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga);

    public Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga);
}