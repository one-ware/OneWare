using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using OneWare.Shared.Models;
using OneWare.Shared.ViewModels;

namespace OneWare.Shared.Services;

public interface IWindowService
{
    public void RegisterUiExtension(string key, Control control);
    public ObservableCollection<Control> GetUiExtensions(string key);
    public void RegisterMenuItem(string key, params MenuItemViewModel[] menuItems);
    public ObservableCollection<MenuItemViewModel> GetMenuItems(string key);
    public void Show(Window window, Window? owner = null);
    public Task ShowDialogAsync(Window window, Window? owner = null);
    public Task ShowMessageAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);
    public Task<MessageBoxStatus> ShowYesNoAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);
    public Task<MessageBoxStatus> ShowYesNoCancelAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);
    public Task<MessageBoxStatus> ShowProceedWarningAsync(string message, Window? owner = null);
    public Task<string?> ShowInputAsync(string title, string message, MessageBoxIcon icon, string? defaultValue, Window? owner = null);
    public Task<string?> ShowFolderSelectAsync(string title, string message, MessageBoxIcon icon, Window? owner = null);
    public Task<object?> ShowInputSelectAsync(string title, string message, MessageBoxIcon icon, IEnumerable<object> options, object? defaultOption, Window? owner = null);
    public void ShowNotification(string title, string message, NotificationType type);
    public void ShowNotificationWithButton(string title, string message, string buttonText, Action buttonAction, IImage? icon = null);
}