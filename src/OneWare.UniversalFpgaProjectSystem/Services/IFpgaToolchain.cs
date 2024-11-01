using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaToolchain
{
    public string Name { get; }

    public void OnProjectCreated(UniversalFpgaProjectRoot project);

    public void LoadConnections(UniversalFpgaProjectRoot project, FpgaModel fpga);

    public void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga);

    public Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga);
    
    public Task<bool> SynthesisAsync(UniversalFpgaProjectRoot project, FpgaModel fpga);
    
    public Task<bool> FitAsync(UniversalFpgaProjectRoot project, FpgaModel fpga);
    
    public Task<bool> AssembleAsync(UniversalFpgaProjectRoot project, FpgaModel fpga);
}