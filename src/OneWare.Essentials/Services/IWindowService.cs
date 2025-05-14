using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Services;

public interface IWindowService
{
    void RegisterUiExtension(string key, UiExtension extension);
    ObservableCollection<UiExtension> GetUiExtensions(string key);
    void RegisterMenuItem(string key, params MenuItemViewModel[] menuItems);
    ObservableCollection<MenuItemViewModel> GetMenuItems(string key);
    void Show(FlexibleWindow window, Window? owner = null);
    Task ShowDialogAsync(FlexibleWindow window, Window? owner = null);
    Task ShowMessageAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);
    Task<MessageBoxStatus> ShowYesNoAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);
    Task<MessageBoxStatus> ShowYesNoCancelAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);
    Task<MessageBoxStatus> ShowProceedWarningAsync(string message, Window? owner = null);
    Task<string?> ShowInputAsync(string title, string message, MessageBoxIcon icon, string? defaultValue, Window? owner = null);
    Task<string?> ShowFolderSelectAsync(string title, string message, MessageBoxIcon icon, string? defaultValue, Window? owner = null);
    Task<object?> ShowInputSelectAsync(string title, string message, MessageBoxIcon icon, IEnumerable<object> options, object? defaultOption, Window? owner = null);
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Information, TimeSpan? expiration = null);
    void ShowNotificationWithButton(string title, string message, string buttonText, Action buttonAction, IImage? icon = null, NotificationType type = NotificationType.Information, TimeSpan? expiration = null);

    // New method to close the main window
    void CloseMainWindow();
}
