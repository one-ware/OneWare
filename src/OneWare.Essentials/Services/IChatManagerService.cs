using Dock.Model.Core;

namespace OneWare.Essentials.Services;

public interface IChatManagerService : IDockable
{
    /// <summary>
    /// Currently selected chat service.
    /// </summary>
    IChatService? SelectedChatService { get; set; }
    /// <summary>
    /// Registers a new chat service.
    /// </summary>
    void RegisterChatService(IChatService chatService);
    /// <summary>
    /// Persists chat service selection/state.
    /// </summary>
    void SaveState();
}
