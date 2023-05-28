using System;
using Avalonia.Controls.Notifications;
using Avalonia.Media;

namespace OneWare.Core.ViewModels.Controls
{
    public class CustomNotificationViewModel : INotification
    {
        public NotificationType Type { get; set; } = NotificationType.Information;
        public TimeSpan Expiration { get; set; } = new(2000);
        public Action? OnClick { get; set; }
        public Action? OnClose { get; set; }
        public Action? OnButtonClick { get; set; }
        public void NotClientImplementable()
        {
            
        }

        public string? Title { get; set; }
        public string? ButtonText { get; set; }
        public string? Message { get; set; }
        public IBrush Background { get; set; } = Brushes.DodgerBlue;
        public IImage? Image { get; set; }

        public void ExecuteButton()
        {
            OnButtonClick?.Invoke();
        }
    }
}