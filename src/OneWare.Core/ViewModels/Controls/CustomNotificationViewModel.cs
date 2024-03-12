using Avalonia.Controls.Notifications;
using Avalonia.Media;

namespace OneWare.Core.ViewModels.Controls
{
    public class CustomNotificationViewModel : Notification
    {
        public Action? ButtonAction { get; set; }
        public string? ButtonText { get; set; }
        public IBrush Background { get; set; } = Brushes.DodgerBlue;
        public IImage? Icon { get; set; }

        public void ExecuteButton()
        {
            ButtonAction?.Invoke();
        }

        public CustomNotificationViewModel(string? title, string? message, string buttonText, Action buttonAction, IImage? icon, NotificationType type = NotificationType.Information, TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null) : base(title, message, type, TimeSpan.FromDays(1), onClick, onClose)
        {
            ButtonText = buttonText;
            ButtonAction = buttonAction;
            Icon = icon;
        }
    }
}