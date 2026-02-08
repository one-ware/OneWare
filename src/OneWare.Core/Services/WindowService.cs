using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using OneWare.Core.ViewModels.Controls;
using OneWare.Core.Views.Windows;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using MessageBoxWindow = OneWare.Core.Views.Windows.MessageBoxWindow;

namespace OneWare.Core.Services;

public class WindowService : IWindowService
{
    private readonly Dictionary<string, ObservableCollection<MenuItemModel>> _menuItems = new();
    private readonly Dictionary<string, ObservableCollection<OneWareUiExtension>> _uiExtensions = new();

    public void RegisterUiExtension(string key, OneWareUiExtension extension)
    {
        _uiExtensions.TryAdd(key, []);
        _uiExtensions[key].Add(extension);
    }

    public ObservableCollection<OneWareUiExtension> GetUiExtensions(string key)
    {
        _uiExtensions.TryAdd(key, []);
        return _uiExtensions[key];
    }

    public void RegisterMenuItem(string key, params MenuItemModel[] menuItems)
    {
        var parts = key.Split('/');
        _menuItems.TryAdd(parts[0], []);
        var activeCollection = _menuItems[parts[0]];

        if (parts.Length > 1)
            foreach (var part in parts.Skip(1))
                if (activeCollection.FirstOrDefault(x => x.PartId == part) is { } mi)
                {
                    activeCollection = mi.Items ?? new ObservableCollection<MenuItemModel>();
                    mi.Items = activeCollection;
                }
                else
                {
                    var newItems = new ObservableCollection<MenuItemModel>();
                    var newPart = new MenuItemModel(part)
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

                var newList = new ObservableCollection<MenuItemModel>();
                if (duplicate.Items != null) Insert(newList, duplicate.Items.ToArray());
                if (a.Items != null) Insert(newList, a.Items.ToArray());
                a.Items = newList;
            }

            Insert(activeCollection, a);
        }
    }

    public ObservableCollection<MenuItemModel> GetMenuItems(string key)
    {
        _menuItems.TryAdd(key, new ObservableCollection<MenuItemModel>());
        return _menuItems[key];
    }

    public void Show(FlexibleWindow window, Window? owner = null)
    {
        owner ??= Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime
            ? null
            : ContainerLocator.Container.Resolve<MainWindow>();
        window.Show(owner);
        window.Focus();
    }

    public async Task ShowDialogAsync(FlexibleWindow window, Window? owner)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            owner ??= ContainerLocator.Container.Resolve<MainWindow>();
            await window.ShowDialogAsync(owner);
        }
        else
        {
            await ContainerLocator.Container.Resolve<MainSingleView>().ShowVirtualDialogAsync(window);
        }

        window.Focus();
    }

    public async Task<MessageBoxResult> ShowMessageBoxAsync(MessageBoxRequest request, Window? owner = null)
    {
        var msg = new MessageBoxWindow(request);
        await ShowDialogAsync(msg, owner);
        return msg.Result;
    }

    public Task ShowMessageAsync(string title, string message, MessageBoxIcon icon, Window? owner)
    {
        var request = new MessageBoxRequest
        {
            Title = title,
            Message = message,
            Icon = icon,
            Buttons = new[]
            {
                new MessageBoxButton
                {
                    Text = "Ok",
                    Role = MessageBoxButtonRole.Yes,
                    Style = MessageBoxButtonStyle.Primary,
                    IsDefault = true
                }
            }
        };

        return ShowDialogAsync(new MessageBoxWindow(request), owner);
    }

    public async Task<MessageBoxStatus> ShowYesNoAsync(string title, string message, MessageBoxIcon icon, Window? owner)
    {
        var request = new MessageBoxRequest
        {
            Title = title,
            Message = message,
            Icon = icon,
            Buttons = new[]
            {
                new MessageBoxButton
                {
                    Text = "Yes",
                    Role = MessageBoxButtonRole.Yes,
                    Style = MessageBoxButtonStyle.Primary,
                    IsDefault = true
                },
                new MessageBoxButton
                {
                    Text = "No",
                    Role = MessageBoxButtonRole.No,
                    Style = MessageBoxButtonStyle.Secondary
                }
            }
        };

        var result = await ShowMessageBoxAsync(request, owner);
        return result.Status;
    }

    public async Task<MessageBoxStatus> ShowYesNoCancelAsync(string title, string message, MessageBoxIcon icon,
        Window? owner)
    {
        var request = new MessageBoxRequest
        {
            Title = title,
            Message = message,
            Icon = icon,
            Buttons = new[]
            {
                new MessageBoxButton
                {
                    Text = "Yes",
                    Role = MessageBoxButtonRole.Yes,
                    Style = MessageBoxButtonStyle.Primary,
                    IsDefault = true
                },
                new MessageBoxButton
                {
                    Text = "No",
                    Role = MessageBoxButtonRole.No,
                    Style = MessageBoxButtonStyle.Secondary
                },
                new MessageBoxButton
                {
                    Text = "Cancel",
                    Role = MessageBoxButtonRole.Cancel,
                    Style = MessageBoxButtonStyle.Secondary
                }
            }
        };

        var result = await ShowMessageBoxAsync(request, owner);
        return result.Status;
    }

    public async Task<MessageBoxStatus> ShowProceedWarningAsync(string message, Window? owner = null)
    {
        var result = await ShowYesNoAsync("Warning", message, MessageBoxIcon.Warning, owner);
        return result;
    }

    public async Task<string?> ShowInputAsync(string title, string message, MessageBoxIcon icon, string? defaultValue,
        Window? owner = null)
    {
        var request = new MessageBoxRequest
        {
            Title = title,
            Message = message,
            Icon = icon,
            Input = new MessageBoxInputOptions
            {
                DefaultValue = defaultValue
            },
            Buttons = new[]
            {
                new MessageBoxButton
                {
                    Text = "Ok",
                    Role = MessageBoxButtonRole.Yes,
                    Style = MessageBoxButtonStyle.Primary,
                    IsDefault = true
                },
                new MessageBoxButton
                {
                    Text = "Cancel",
                    Role = MessageBoxButtonRole.Cancel,
                    Style = MessageBoxButtonStyle.Secondary
                }
            }
        };

        var result = await ShowMessageBoxAsync(request, owner);
        return result.IsCanceled ? null : result.Input;
    }

    public async Task<string?> ShowFolderSelectAsync(string title, string message, MessageBoxIcon icon,
        string? defaultValue,
        Window? owner = null)
    {
        var request = new MessageBoxRequest
        {
            Title = title,
            Message = message,
            Icon = icon,
            Input = new MessageBoxInputOptions
            {
                DefaultValue = defaultValue,
                ShowFolderButton = true
            },
            Buttons = new[]
            {
                new MessageBoxButton
                {
                    Text = "Ok",
                    Role = MessageBoxButtonRole.Yes,
                    Style = MessageBoxButtonStyle.Primary,
                    IsDefault = true
                },
                new MessageBoxButton
                {
                    Text = "Cancel",
                    Role = MessageBoxButtonRole.Cancel,
                    Style = MessageBoxButtonStyle.Secondary
                }
            }
        };

        var result = await ShowMessageBoxAsync(request, owner);
        return result.IsCanceled ? null : result.Input;
    }

    public async Task<object?> ShowInputSelectAsync(string title, string message, MessageBoxIcon icon,
        IEnumerable<object> options, object? defaultOption, Window? owner = null)
    {
        var request = new MessageBoxRequest
        {
            Title = title,
            Message = message,
            Icon = icon,
            SelectionItems = options.ToArray(),
            SelectedItem = defaultOption,
            Buttons = new[]
            {
                new MessageBoxButton
                {
                    Text = "Ok",
                    Role = MessageBoxButtonRole.Yes,
                    Style = MessageBoxButtonStyle.Primary,
                    IsDefault = true
                },
                new MessageBoxButton
                {
                    Text = "Cancel",
                    Role = MessageBoxButtonRole.Cancel,
                    Style = MessageBoxButtonStyle.Secondary
                }
            }
        };

        var result = await ShowMessageBoxAsync(request, owner);
        return result.IsCanceled ? null : result.SelectedItem;
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information,
        TimeSpan? expiration = null)
    {
        var model = new CustomNotificationViewModel(title, message, type, expiration ?? TimeSpan.FromSeconds(5));

        ContainerLocator.Container.Resolve<MainWindow>().NotificationManager
            ?.Show(model);
    }

    public void ShowNotificationWithButton(string title, string message, string buttonText,
        Action buttonAction, IImage? icon = null, NotificationType type = NotificationType.Information,
        TimeSpan? expiration = null)
    {
        var model = new CustomNotificationViewModel(title, message, type, expiration ?? TimeSpan.FromSeconds(10),
            buttonText, buttonAction, icon);

        ContainerLocator.Container.Resolve<MainWindow>().NotificationManager?.Show(model);
    }

    public void ActivateMainWindow()
    {
        ContainerLocator.Container.Resolve<MainWindow>().Activate();
    }

    private static void Insert(IList<MenuItemModel> collection, params MenuItemModel[] items)
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
}