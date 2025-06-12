using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia;

namespace OneWare.Core.Extensions
{

    public static class Extensions
    {
        private static readonly AttachedProperty<INotificationManager> NotificationManagerProperty =
            AvaloniaProperty.RegisterAttached<Window, INotificationManager>("NotificationManager", typeof(Extensions));

        public static INotificationManager GetNotificationManager(this Window window)
        {
            return window.GetValue(NotificationManagerProperty);
        }

        public static void SetNotificationManager(this Window window, INotificationManager value)
        {
            window.SetValue(NotificationManagerProperty, value);
        }
        public static bool IsDefault<T>(this T value) where T : struct
        {
            var isDefault = value.Equals(default(T));

            return isDefault;
        }

        public static bool IsNullOrEmpty(this IEnumerable? source)
        {
            if (source != null)
                foreach (var obj in source)
                    return false;
            return true;
        }

        public static void Each<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items) action(item);
        }
    }
}