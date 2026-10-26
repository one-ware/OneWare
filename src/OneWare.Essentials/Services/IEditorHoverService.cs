using OneWare.Essentials.EditorExtensions;

namespace OneWare.Essentials.Services;

/// <summary>
/// Collects <see cref="IEditorHoverProvider" />s that editors ask before the language server
/// hover, e.g. to show debugger values while a session is halted.
/// </summary>
public interface IEditorHoverService
{
    /// <summary>
    /// Adds a provider. Providers are asked in registration order; the first non-null content wins.
    /// </summary>
    public void RegisterProvider(IEditorHoverProvider provider);

    /// <summary>
    /// Asks the registered providers for hover content. Returns <see langword="null"/> if none answers.
    /// </summary>
    public Task<object?> GetHoverContentAsync(EditorHoverRequest request);
}
