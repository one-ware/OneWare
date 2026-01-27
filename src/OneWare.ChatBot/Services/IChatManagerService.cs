using Dock.Model.Core;

namespace OneWare.ChatBot.Services;

public interface IChatManagerService : IDockable
{
    public void RegisterChat(IChatService chatService);
}