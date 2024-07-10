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
    public void RegisterUiExtension(string key, UiExtension extension);
    public ObservableCollection<UiExtension> GetUiExtensions(string key);
    public void RegisterMenuItem(string key, params MenuItemViewModel[] menuItems);
    public ObservableCollection<MenuItemViewModel> GetMenuItems(string key);
    public void Show(FlexibleWindow window, Window? owner = null);
    public Task ShowDialogAsync(FlexibleWindow window, Window? owner = null);
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