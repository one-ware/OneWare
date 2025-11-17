using OneWare.Essentials.Enums;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProviderRegistry
{
    void Register(LanguageType type,  INodeProvider nodeProvider);
    
    void Register<TNodeProvider>(LanguageType type) where TNodeProvider : INodeProvider;
    
    void Unregister(LanguageType type, string nodeExporterKey);
    
    List<INodeProvider> GetNodeProviders(LanguageType type);

    INodeProvider GetNodeProvider(LanguageType type);
}