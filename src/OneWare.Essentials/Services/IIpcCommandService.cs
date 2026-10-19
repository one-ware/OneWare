namespace OneWare.Essentials.Services;

/// <summary>
/// Handles commands sent to the running OneWare Studio over <see cref="Helpers.StudioInstanceChannel" />
/// (for example by CLI modules). Handlers run on the UI thread.
/// </summary>
public interface IIpcCommandService
{
    /// <summary>
    /// Registers a handler for a command name. A later registration replaces an earlier one.
    /// </summary>
    void RegisterCommand(string name, Func<string?, Task> handler);

    /// <summary>
    /// Runs the handler registered for <paramref name="name" />. Returns false if no handler is registered.
    /// </summary>
    Task<bool> ExecuteAsync(string name, string? argument);
}
