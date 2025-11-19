using OneWare.Essentials.Enums;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface INodeProviderRegistry
{
    void Register(string type,  INodeProvider nodeProvider);
    
    void Register<TNodeProvider>(string type) where TNodeProvider : INodeProvider;
    
    void Unregister(string type, string nodeExporterKey);
    
    List<INodeProvider> GetNodeProviders(string type);

    INodeProvider GetNodeProvider(string type);
}