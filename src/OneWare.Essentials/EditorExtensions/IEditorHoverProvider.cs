using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.EditorExtensions;

/// <summary>
/// Describes the position the pointer rests on in an editor.
/// </summary>
/// <param name="Editor">The editor that is hovered.</param>
/// <param name="Offset">Document offset under the pointer.</param>
/// <param name="Word">Word under the pointer.</param>
public sealed record EditorHoverRequest(IEditor Editor, int Offset, string Word);

/// <summary>
/// Supplies hover popup content for editors, ahead of the language server hover.
/// Register it with <see cref="Services.IEditorHoverService.RegisterProvider"/>.
/// </summary>
public interface IEditorHoverProvider
{
    /// <summary>
    /// Returns the popup content (a control, or a view model with a matching data template), or
    /// <see langword="null"/> to let the next provider or the language server answer.
    /// Called on the UI thread.
    /// </summary>
    public Task<object?> GetHoverContentAsync(EditorHoverRequest request);
}
