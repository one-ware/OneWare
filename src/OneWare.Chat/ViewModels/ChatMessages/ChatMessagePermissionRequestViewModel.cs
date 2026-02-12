using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Chat.ViewModels.ChatMessages;

public class ChatMessagePermissionRequestViewModel : ObservableObject, IChatMessage
{
    public ChatMessagePermissionRequestViewModel(ChatPermissionRequestEvent permissionRequestEvent)
    {
        Event = permissionRequestEvent;
        IsVisible = true;
    }

    public ChatPermissionRequestEvent Event { get; }

    public bool HasAllowForSession =>
        Event.AllowForSessionCommand != null &&
        !string.IsNullOrWhiteSpace(Event.AllowForSessionButtonText);

    public bool IsVisible
    {
        get;
        set => SetProperty(ref field, value);
    }

    public void Allow()
    {
        Event.AllowCommand?.Execute(null);
        IsVisible = false;
    }
    
    public void AllowForSession()
    {
        Event.AllowForSessionCommand?.Execute(null);
        IsVisible = false;
    }
    
    public void Deny()
    {
        Event.DenyCommand?.Execute(null);
        IsVisible = false;
    }
}
