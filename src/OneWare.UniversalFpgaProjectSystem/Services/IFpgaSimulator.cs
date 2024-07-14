using OneWare.Essentials.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaSimulator
{
    public string Name { get; }

    public UiExtension? TestBenchToolbarTopUiExtension { get; }

    public Task<bool> SimulateAsync(IFile file);
}