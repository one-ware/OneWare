using OneWare.Essentials.Enums;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProviderRegistry
{
    void Register(LanguageType type,  INodeProvider nodeProvider);
    
    void Unregister(LanguageType type, string nodeExporterKey);
    
    List<INodeProvider> GetNodeProviders(LanguageType type);
}