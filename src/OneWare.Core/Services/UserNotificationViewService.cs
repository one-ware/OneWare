using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class UserNotificationViewService : IUserNotificationReceiver
{
    private readonly IOutputService _outputService;
    private readonly IWindowService _windowService;
    private readonly IDockService _dockService;

    public UserNotificationViewService(IOutputService outputService, 
        IDockService dockService,
        IWindowService windowService)
    {
        _outputService = outputService;
        _windowService = windowService;
        _dockService = dockService;
    }

    public void Attach()
    {
        UserNotificationEventMessenger.RegisterReceiver(this);
    }
    public void Receive(UserNotification notification)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (notification.ShowOutput)
            {
                var outputBrush = notification.OutputBrush ?? notification.NotificationKind switch
                {
                    UserNotificationKind.Warning => Brushes.Orange,
                    UserNotificationKind.Error => Brushes.Red,
                    _ => null
                };
                
                _outputService.WriteLine(notification.Message, outputBrush);
                _dockService.Show(_outputService);
            }
            
            if (notification.ShowWindow) 
            {
                var icon = notification.NotificationKind switch
                {
                    UserNotificationKind.Warning => MessageBoxIcon.Warning,
                    UserNotificationKind.Error => MessageBoxIcon.Error,
                    _ => MessageBoxIcon.Info
                };
                var title = notification.NotificationKind switch
                {
                    UserNotificationKind.Warning => "Warning",
                    UserNotificationKind.Error => "Error",
                    _ => "Information"
                };
                _ = _windowService.ShowMessageAsync(title, notification.Message, icon, notification.WindowOwner);
            }
        });
    }
}