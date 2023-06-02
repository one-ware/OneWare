using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Core.ViewModels.Controls;
using OneWare.Core.Views.Windows;
using Prism.Ioc;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using OneWare.Shared.Views;

namespace OneWare.Core.Services;

public class WindowService : IWindowService
{
    private readonly Dictionary<string, ObservableCollection<MenuItemViewModel>> _menuItems = new();
    private readonly Dictionary<string, ObservableCollection<Control>> _uiExtensions = new();
    
    public void RegisterUiExtension(string key, Control control)
    {
        _uiExtensions.TryAdd(key, new ObservableCollection<Control>());
        _uiExtensions[key].Add(control);
    }

    public ObservableCollection<Control> GetUiExtensions(string key)
    { 
        _uiExtensions.TryAdd(key, new ObservableCollection<Control>());
        return _uiExtensions[key];
    }

    public void RegisterMenuItem(string key, params MenuItemViewModel[] menuItems)
    {
        var parts = key.Split('/');
        _menuItems.TryAdd(parts[0], new ObservableCollection<MenuItemViewModel>());
        IList<MenuItemViewModel> activeCollection = _menuItems[parts[0]];
        
        if(parts.Length > 1)
            foreach (var part in parts.Skip(1))
            {
                if (activeCollection.FirstOrDefault(x => x.Header == part) is { } mi)
                {
                    activeCollection = mi.Items ?? new ObservableCollection<MenuItemViewModel>();
                    mi.Items = activeCollection;
                }
                else
                {
                    var newItems = new ObservableCollection<MenuItemViewModel>();
                    var newPart = new MenuItemViewModel()
                    {
                        Header = part,
                        Items = newItems
                    };
                    var insert = false;
                    for(var i = 0; i < activeCollection.Count; i++)
                    {
                        if (activeCollection[i].Priority >= 0)
                        {
                            activeCollection.Insert(i, newPart);
                            insert = true;
                            break;
                        }
                    }
                    if(!insert) activeCollection.Add(newPart);
                    activeCollection = newItems;
                }
            }

        if (menuItems.Length == 0) return;
        
        foreach (var a in menuItems)
        {
            var insert = false;
            
            if (activeCollection.FirstOrDefault(x => x.Header == a.Header) is {} duplicate)
            {
                activeCollection.Remove(duplicate);
            }
            
            for(var i = 0; i < activeCollection.Count; i++)
            {
                if (a.Priority <= activeCollection[i].Priority)
                {
                    activeCollection.Insert(i, a);
                    insert = true;
                    break;
                }
            }
            if(!insert) activeCollection.Add(a);
        }
    }

    public ObservableCollection<MenuItemViewModel> GetMenuItems(string key)
    {
        _menuItems.TryAdd(key, new ObservableCollection<MenuItemViewModel>());
        return _menuItems[key];
    }

    public void Show(Window window, Window? owner = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            owner ??= ContainerLocator.Container.Resolve<MainWindow>();
            window.Show(owner);
        });
    }

    public Task ShowDialogAsync(Window window, Window? owner)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            owner ??= ContainerLocator.Container.Resolve<MainWindow>();
            return window.ShowDialog(owner);
        });
    }
    
    public Task ShowMessageAsync(string title, string message, MessageBoxIcon icon, Window? owner)
    {
        return ShowDialogAsync(new MessageBoxWindow(title, message, MessageBoxMode.OnlyOk, MessageBoxIcon.Info),owner);
    }

    public async Task<MessageBoxStatus> ShowYesNoAsync(string title, string message, MessageBoxIcon icon, Window? owner)
    {
        var msg = new MessageBoxWindow(title, message, MessageBoxMode.NoCancel, icon);
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus;
    }
    
    public async Task<MessageBoxStatus> ShowYesNoCancelAsync(string title, string message, MessageBoxIcon icon, Window? owner)
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

    public async Task<string?> ShowInputAsync(string title, string message, MessageBoxIcon icon, string? defaultValue, Window? owner = null)
    {
        var msg = new MessageBoxWindow(title, message,
            MessageBoxMode.Input, icon);
        msg.Input = defaultValue;
        await ShowDialogAsync(msg, owner);
        if (msg.BoxStatus == MessageBoxStatus.Canceled) return null;
        return msg.Input;
    }

    public async Task<string?> ShowFolderSelectAsync(string title, string message, MessageBoxIcon icon, Window? owner = null)
    {
        var msg = new MessageBoxWindow(title, message,
            MessageBoxMode.SelectFolder, icon);
        await ShowDialogAsync(msg, owner);
        if (msg.BoxStatus == MessageBoxStatus.Canceled) return null;
        return msg.Input;
    }

    public async Task<object?> ShowInputSelectAsync(string title, string message, MessageBoxIcon icon, IEnumerable<object> options, object? defaultOption, Window? owner = null)
    {
        var msg = new MessageBoxWindow(title, message,
            MessageBoxMode.SelectItem, MessageBoxIcon.Info);
        msg.SelectionItems.AddRange(options);
        msg.SelectedItem = defaultOption;
        await ShowDialogAsync(msg, owner);
        if (msg.BoxStatus == MessageBoxStatus.Canceled) return msg.SelectedItem;
        return null;
    }

    public void ShowNotification(string title, string message, NotificationType type)
    {
        ContainerLocator.Container.Resolve<MainWindow>().NotificationManager.Show(new Notification(title, message, type));
    }

    public void ShowNotificationWithButton(string title, string message, string buttonText, Action buttonAction, IImage? icon = null)
    {
        var model = new CustomNotificationViewModel(title, message, buttonText, buttonAction, icon);

        ContainerLocator.Container.Resolve<MainWindow>().NotificationManager.Show(model);
    }
}