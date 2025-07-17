namespace OneWare.Essentials.Services;

public static class UserNotificationEventMessenger
{
    private static IUserNotificationReceiver? _userNotificationReceiver;

    public static void RegisterReceiver(IUserNotificationReceiver receiver)
    {
        _userNotificationReceiver = receiver;
    }
    public static void Send(UserNotification notification)
    {
        _userNotificationReceiver?.Receive(notification);
    }
}

public interface IUserNotificationReceiver
{
    void Receive(UserNotification notification);
}



