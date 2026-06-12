using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaPreCompileStep
{
    public string Id => Name;
    
    public string Name { get; }

    public Task<bool> PerformPreCompileStepAsync(UniversalFpgaProjectRoot project, FpgaModel fpga);
}