using Microsoft.Extensions.Logging;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class EditorHoverService(ILogger logger) : IEditorHoverService
{
    private readonly List<IEditorHoverProvider> _providers = [];

    public void RegisterProvider(IEditorHoverProvider provider)
    {
        if (!_providers.Contains(provider)) _providers.Add(provider);
    }

    public async Task<object?> GetHoverContentAsync(EditorHoverRequest request)
    {
        foreach (var provider in _providers.ToArray())
            try
            {
                if (await provider.GetHoverContentAsync(request) is { } content) return content;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Editor hover provider {Provider} failed", provider.GetType().Name);
            }

        return null;
    }
}
