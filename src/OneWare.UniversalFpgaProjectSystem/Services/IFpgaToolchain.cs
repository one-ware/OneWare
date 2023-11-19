using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaToolchain
{
    public string Name { get; }

    public void LoadConnections(UniversalFpgaProjectRoot project, FpgaModel fpga);

    public void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga);

    public void StartCompile(UniversalFpgaProjectRoot project, FpgaModel fpga);
}