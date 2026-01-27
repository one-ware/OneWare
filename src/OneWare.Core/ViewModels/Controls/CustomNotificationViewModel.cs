using Avalonia.Controls.Notifications;
using Avalonia.Media;

namespace OneWare.Core.ViewModels.Controls;

public class CustomNotificationViewModel : Notification
{
    public CustomNotificationViewModel(string title, string message, NotificationType type, TimeSpan expiration,
        string? buttonText = null, Action? buttonAction = null,
        IImage? icon = null, Action? onClick = null, Action? onClose = null) : base(title, message, type, expiration,
        onClick,
        onClose)
    {
        ButtonText = buttonText;
        ButtonAction = buttonAction;
        Icon = icon;
    }

    public Action? ButtonAction { get; set; }
    public string? ButtonText { get; set; }

    public IBrush Background { get; set; } = Brushes.DodgerBlue;
    public IImage? Icon { get; set; }

    public void ExecuteButton()
    {
        ButtonAction?.Invoke();
    }
}