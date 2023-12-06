using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Utils;
using DynamicData;
using OneWare.Core.ViewModels.Controls;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.SDK.Controls;
using OneWare.SDK.Enums;
using Prism.Ioc;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using MessageBoxWindow = OneWare.Core.Views.Windows.MessageBoxWindow;

namespace OneWare.Core.Services;

public class WindowService : IWindowService
{
    private readonly Dictionary<string, ObservableCollection<IMenuItem>> _menuItems = new();
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

    public void RegisterMenuItem(string key, params IMenuItem[] menuItems)
    {
        var parts = key.Split('/');
        _menuItems.TryAdd(parts[0], new ObservableCollection<IMenuItem>());
        IList<IMenuItem> activeCollection = _menuItems[parts[0]];
        
        if(parts.Length > 1)
            foreach (var part in parts.Skip(1))
            {
                if (activeCollection.FirstOrDefault(x => x.Part == part) is MenuItemViewModel mi)
                {
                    activeCollection = mi.Items ?? new ObservableCollection<IMenuItem>();
                    mi.Items = activeCollection;
                }
                else
                {
                    var newItems = new ObservableCollection<IMenuItem>();
                    var newPart = new MenuItemViewModel(part)
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
            
            if (activeCollection.FirstOrDefault(x => x.Part == a.Part) is {} duplicate)
            {
                activeCollection.Remove(duplicate);
                
                //TODO Improve duplicate handling
                if (a is MenuItemViewModel av && duplicate is MenuItemViewModel dv)
                {
                    var newList = new ObservableCollection<IMenuItem>();
                    if(dv.Items != null) newList.AddRange(dv.Items);
                    if(av.Items != null) newList.AddRange(av.Items);
                    av.Items = newList;
                }
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

    public ObservableCollection<IMenuItem> GetMenuItems(string key)
    {
        _menuItems.TryAdd(key, new ObservableCollection<IMenuItem>());
        return _menuItems[key];
    }

    public void Show(FlexibleWindow window, Window? owner = null)
    {
        owner ??= Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime ? null : ContainerLocator.Container.Resolve<MainWindow>();
        window.Show(owner);
    }

    public Task ShowDialogAsync(FlexibleWindow window, Window? owner)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            owner ??= ContainerLocator.Container.Resolve<MainWindow>();
            return window.ShowDialogAsync(owner);
        }
        return ContainerLocator.Container.Resolve<MainView>().ShowVirtualDialogAsync(window);
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