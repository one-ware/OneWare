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
    /// <summary>
    /// Registers a UI extension factory under a named slot.
    /// </summary>
    public void RegisterUiExtension(string key, OneWareUiExtension extension);
    /// <summary>
    /// Returns all UI extensions registered for a slot.
    /// </summary>
    public ObservableCollection<OneWareUiExtension> GetUiExtensions(string key);
    /// <summary>
    /// Registers one or more menu items under a menu path.
    /// </summary>
    public void RegisterMenuItem(string key, params MenuItemModel[] menuItems);
    /// <summary>
    /// Returns the menu items registered for a menu path.
    /// </summary>
    public ObservableCollection<MenuItemModel> GetMenuItems(string key);
    /// <summary>
    /// Shows a non-modal window.
    /// </summary>
    public void Show(FlexibleWindow window, Window? owner = null);
    /// <summary>
    /// Shows a modal dialog window.
    /// </summary>
    public Task ShowDialogAsync(FlexibleWindow window, Window? owner = null);
    /// <summary>
    /// Shows a message box and returns the result.
    /// </summary>
    public Task<MessageBoxResult> ShowMessageBoxAsync(MessageBoxRequest request, Window? owner = null);
    /// <summary>
    /// Shows a message box with a single acknowledgment button.
    /// </summary>
    public Task ShowMessageAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);

    /// <summary>
    /// Shows a Yes/No dialog.
    /// </summary>
    public Task<MessageBoxStatus> ShowYesNoAsync(string title, string message, MessageBoxIcon icon,
        Window? owner = null);

    /// <summary>
    /// Shows a Yes/No/Cancel dialog.
    /// </summary>
    public Task<MessageBoxStatus> ShowYesNoCancelAsync(string title, string message, MessageBoxIcon icon,
        Window? owner = null);

    /// <summary>
    /// Shows a proceed warning dialog.
    /// </summary>
    public Task<MessageBoxStatus> ShowProceedWarningAsync(string message, Window? owner = null);

    /// <summary>
    /// Shows an input dialog and returns the entered value.
    /// </summary>
    public Task<string?> ShowInputAsync(string title, string message, MessageBoxIcon icon, string? defaultValue,
        Window? owner = null);

    /// <summary>
    /// Shows a folder selection dialog and returns the chosen path.
    /// </summary>
    public Task<string?> ShowFolderSelectAsync(string title, string message, MessageBoxIcon icon, string? defaultValue,
        Window? owner = null);

    /// <summary>
    /// Shows a selection dialog and returns the chosen option.
    /// </summary>
    public Task<object?> ShowInputSelectAsync(string title, string message, MessageBoxIcon icon,
        IEnumerable<object> options, object? defaultOption, Window? owner = null);

    /// <summary>
    /// Shows a toast notification.
    /// </summary>
    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information,
        TimeSpan? expiration = null);

    /// <summary>
    /// Shows a toast notification with a button action.
    /// </summary>
    public void ShowNotificationWithButton(string title, string message, string buttonText,
        Action buttonAction, IImage? icon = null, NotificationType type = NotificationType.Information,
        TimeSpan? expiration = null);

    /// <summary>
    /// Brings the main window to the foreground.
    /// </summary>
    public void ActivateMainWindow();
}
