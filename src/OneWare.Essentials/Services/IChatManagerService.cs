using Dock.Model.Core;

namespace OneWare.Essentials.Services;

public interface IChatManagerService : IDockable
{
    IChatService? SelectedChatService { get; set; }
    void RegisterChatService(IChatService chatService);
}