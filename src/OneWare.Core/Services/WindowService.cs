﻿using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using OneWare.Core.ViewModels.Controls;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Controls;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OneWare.Essentials.Models;

namespace OneWare.Core.Services;

public class WindowService : IWindowService
{
    private readonly Dictionary<string, ObservableCollection<MenuItemViewModel>> _menuItems = new();
    private readonly Dictionary<string, ObservableCollection<UiExtension>> _uiExtensions = new();
    private readonly INotificationManager _notificationManager;  // Dependency for notifications

    public WindowService(INotificationManager notificationManager)
    {
        _notificationManager = notificationManager;
    }

    public void RegisterUiExtension(string key, UiExtension extension)
    {
        _uiExtensions.TryAdd(key, new ObservableCollection<UiExtension>());
        _uiExtensions[key].Add(extension);
    }

    public ObservableCollection<UiExtension> GetUiExtensions(string key)
    {
        _uiExtensions.TryAdd(key, new ObservableCollection<UiExtension>());
        return _uiExtensions[key];
    }

    public void RegisterMenuItem(string key, params MenuItemViewModel[] menuItems)
    {
        var parts = key.Split('/');
        _menuItems.TryAdd(parts[0], new ObservableCollection<MenuItemViewModel>());
        var activeCollection = _menuItems[parts[0]];

        if (parts.Length > 1)
            foreach (var part in parts.Skip(1))
                if (activeCollection.FirstOrDefault(x => x.PartId == part) is { } mi)
                {
                    activeCollection = mi.Items ?? new ObservableCollection<MenuItemViewModel>();
                    mi.Items = activeCollection;
                }
                else
                {
                    var newItems = new ObservableCollection<MenuItemViewModel>();
                    var newPart = new MenuItemViewModel(part)
                    {
                        Header = part,
                        Items = newItems
                    };
                    var insert = false;
                    for (var i = 0; i < activeCollection.Count; i++)
                        if (activeCollection[i].Priority >= 0)
                        {
                            activeCollection.Insert(i, newPart);
                            insert = true;
                            break;
                        }

                    if (!insert) activeCollection.Add(newPart);
                    activeCollection = newItems;
                }

        if (menuItems.Length == 0) return;

        foreach (var a in menuItems)
        {
            if (activeCollection.FirstOrDefault(x => x.PartId == a.PartId) is { } duplicate)
            {
                activeCollection.Remove(duplicate);

                var newList = new ObservableCollection<MenuItemViewModel>();
                if (duplicate.Items != null) Insert(newList, duplicate.Items.ToArray());
                if (a.Items != null) Insert(newList, a.Items.ToArray());
                a.Items = newList;
            }

            Insert(activeCollection, a);
        }
    }

    public ObservableCollection<MenuItemViewModel> GetMenuItems(string key)
    {
        _menuItems.TryAdd(key, new ObservableCollection<MenuItemViewModel>());
        return _menuItems[key];
    }

    public void Show(FlexibleWindow window, Window? owner = null)
    {
        owner ??= GetMainWindow();  // Directly obtain the main window
        window.Show(owner);
        window.Focus();
    }

    public async Task ShowDialogAsync(FlexibleWindow window, Window? owner)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            owner ??= GetMainWindow();  // Directly obtain the main window
            await window.ShowDialogAsync(owner);
        }
        else await ShowVirtualDialogAsync(window);  // Handle virtual dialogs (if applicable)

        window.Focus();
    }

    public Task ShowMessageAsync(string title, string message, MessageBoxIcon icon, Window? owner)
    {
        return ShowDialogAsync(new MessageBoxWindow(title, message, MessageBoxMode.OnlyOk, MessageBoxIcon.Info), owner);
    }

    public async Task<MessageBoxStatus> ShowYesNoAsync(string title, string message, MessageBoxIcon icon, Window? owner)
    {
        var msg = new MessageBoxWindow(title, message, MessageBoxMode.NoCancel, icon);
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus;
    }

    public async Task<MessageBoxStatus> ShowYesNoCancelAsync(string title, string message, MessageBoxIcon icon,
        Window? owner)
    {
        var msg = new MessageBoxWindow(title, message, MessageBoxMode.AllButtons, icon);
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus;
    }

    public async Task<MessageBoxStatus> ShowProceedWarningAsync(string message, Window? owner = null)
    {
        var msg = new MessageBoxWindow("Warning", message, MessageBoxMode.NoCancel);
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus;
    }

    public async Task<string?> ShowInputAsync(string title, string message, MessageBoxIcon icon, string? defaultValue,
        Window? owner = null)
    {
        var msg = new MessageBoxWindow(title, message,
            MessageBoxMode.Input, icon)
        {
            Input = defaultValue
        };
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus == MessageBoxStatus.Canceled ? null : msg.Input;
    }

    public async Task<string?> ShowFolderSelectAsync(string title, string message, MessageBoxIcon icon, string? defaultValue,
        Window? owner = null)
    {
        var msg = new MessageBoxWindow(title, message,
            MessageBoxMode.SelectFolder, icon)
        {
            Input = defaultValue
        };
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus == MessageBoxStatus.Canceled ? null : msg.Input;
    }

    public async Task<object?> ShowInputSelectAsync(string title, string message, MessageBoxIcon icon,
        IEnumerable<object> options, object? defaultOption, Window? owner = null)
    {
        var msg = new MessageBoxWindow(title, message,
            MessageBoxMode.SelectItem, MessageBoxIcon.Info);
        msg.SelectionItems.AddRange(options);
        msg.SelectedItem = defaultOption;
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus != MessageBoxStatus.Canceled ? msg.SelectedItem : null;
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information,
        TimeSpan? expiration = null)
    {
        var model = new CustomNotificationViewModel(title, message, type, expiration ?? TimeSpan.FromSeconds(5));

        _notificationManager?.Show(model);  // Use injected notification manager
    }

    public void ShowNotificationWithButton(string title, string message, string buttonText,
        Action buttonAction, IImage? icon = null, NotificationType type = NotificationType.Information,
        TimeSpan? expiration = null)
    {
        var model = new CustomNotificationViewModel(title, message, type, expiration ?? TimeSpan.FromSeconds(10), buttonText, buttonAction, icon);

        _notificationManager?.Show(model);  // Use injected notification manager
    }

    private static void Insert(IList<MenuItemViewModel> collection, params MenuItemViewModel[] items)
    {
        foreach (var item in items)
        {
            var insert = false;
            for (var i = 0; i < collection.Count; i++)
                if (item.Priority <= collection[i].Priority)
                {
                    collection.Insert(i, item);
                    insert = true;
                    break;
                }

            if (!insert) collection.Add(item);
        }
    }

    // Helper method to get the main window (or resolve it from DI, if needed)
    private Window GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktopApp => desktopApp.MainWindow,
            _ => throw new InvalidOperationException("Unable to resolve main window.")
        };
    }

    // Placeholder for handling virtual dialogs, if necessary
    private async Task ShowVirtualDialogAsync(FlexibleWindow window)
    {
        // Custom handling for virtual dialogs can be implemented here
        await Task.CompletedTask;
    }

    // Method to close the main window
    public void CloseMainWindow()
    {
        var mainWindow = GetMainWindow();
        mainWindow?.Close();
    }
}
