using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessagePermissionRequestViewModel : ObservableObject, IChatMessage
{
    public ChatMessagePermissionRequestViewModel(ChatPermissionRequestEvent permissionRequestEvent)
    {
        Event = permissionRequestEvent;
    }

    public ChatPermissionRequestEvent Event { get; }
}
