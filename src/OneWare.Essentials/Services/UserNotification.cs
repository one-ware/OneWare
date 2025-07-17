using Avalonia.Controls;
using Avalonia.Media;

namespace OneWare.Essentials.Services;

public class UserNotification
{
    private UserNotification(string message, UserNotificationKind notificationKind)
    {
        NotificationKind = notificationKind;
        Message = message;  
    }
        
    public string Message { get; private set; }
    public bool ShowOutput { get; private set; }
    public bool ShowWindow{ get; private set; }
    public UserNotificationKind NotificationKind{ get; private set; }
    public Window? WindowOwner{ get; private set; }
    public IBrush? OutputBrush{ get; private set; }
        
    public static UserNotification NewInformation(string message)
    {
        return new UserNotification(message, UserNotificationKind.Information);
    }
    
    public static UserNotification NewWarning(string message)
    {
        return new UserNotification(message, UserNotificationKind.Warning);
    }

    public static UserNotification NewError(string message)
    {
        return new UserNotification(message, UserNotificationKind.Error);
    }

    public UserNotification ViaOutput(IBrush? brush = null)
    {
        ShowOutput = true;
        OutputBrush = brush;
        return this;
    }

    public UserNotification ViaWindow(Window? windowOwner = null)
    {
        ShowWindow = true;
        WindowOwner = windowOwner;
        return this;
    }

    public void Send()
    {
        if (!ShowOutput && !ShowWindow)
            return;

        UserNotificationEventMessenger.Send(this);
    }
}

public enum UserNotificationKind
{
    Information,
    Warning,
    Error
}



