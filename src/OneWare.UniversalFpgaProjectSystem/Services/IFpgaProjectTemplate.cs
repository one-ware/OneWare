using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaProjectTemplate
{
    string Name { get; }

    void FillTemplate(UniversalFpgaProjectRoot root);
}