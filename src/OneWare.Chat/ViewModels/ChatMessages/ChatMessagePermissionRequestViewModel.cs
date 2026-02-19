using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessagePermissionRequestViewModel : ObservableObject, IChatMessage
{
    public ChatMessagePermissionRequestViewModel(ChatPermissionRequestEvent permissionRequestEvent)
    {
        Event = permissionRequestEvent;
    }
    
    public Action? CloseAction { get; set; }
    
    public ChatPermissionRequestEvent Event { get; }

    public bool HasAllowForSession =>
        Event.AllowForSessionCommand != null &&
        !string.IsNullOrWhiteSpace(Event.AllowForSessionButtonText);

    public void Allow()
    {
        Event.AllowCommand?.Execute(null);
        CloseAction?.Invoke();
    }
    
    public void AllowForSession()
    {
        Event.AllowForSessionCommand?.Execute(null);
        CloseAction?.Invoke();
    }
    
    public void Deny()
    {
        Event.DenyCommand?.Execute(null);
        CloseAction?.Invoke();
    }
}
