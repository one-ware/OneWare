namespace OneWare.UniversalFpgaProjectSystem.Services;

public class NodeProviderService
{
    private readonly Dictionary<string, INodeProvider> _providers = new();
    
    public void RegisterNodeProvider(INodeProvider provider, params string[] extensions)
    {
        foreach (var ext in extensions)
        {
            _providers[ext] = provider;
        }
    }

    public INodeProvider? GetNodeProvider(string extension)
    {
        return _providers.TryGetValue(extension, out var provider) ? provider : null;
    }
}