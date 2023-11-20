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
using OneWare.Core.ViewModels.Controls;
using OneWare.Core.ViewModels.Windows;
using OneWare.Core.Views.Windows;
using OneWare.Shared.Controls;
using OneWare.Shared.Enums;
using Prism.Ioc;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
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
                if (activeCollection.FirstOrDefault(x => x.Part == part) is MenuItemModel mi)
                {
                    activeCollection = mi.Items ?? new ObservableCollection<IMenuItem>();
                    mi.Items = activeCollection;
                }
                else
                {
                    var newItems = new ObservableCollection<IMenuItem>();
                    var newPart = new MenuItemModel(part)
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
        Dispatcher.UIThread.Post(() =>
        {
            owner ??= Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime ? null : ContainerLocator.Container.Resolve<MainWindow>();
            window.Show(owner);
        });
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
        var r = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var msg = new MessageBoxWindow(title, message, MessageBoxMode.AllButtons, icon);
            await ShowDialogAsync(msg, owner);
            return msg.BoxStatus;
        });
        return r;
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
        Dispatcher.UIThread.Post(() =>
        {
            ContainerLocator.Container.Resolve<MainWindow>().NotificationManager.Show(new Notification(title, message, type));
        });
    }

    public void ShowNotificationWithButton(string title, string message, string buttonText, Action buttonAction, IImage? icon = null)
    {
        var model = new CustomNotificationViewModel(title, message, buttonText, buttonAction, icon);

        ContainerLocator.Container.Resolve<MainWindow>().NotificationManager.Show(model);
    }

    public Window CreateHost(FlexibleWindow flexible)
    {
        var host = new AdvancedWindow();
            
        host.Bind(AdvancedWindow.ShowTitleProperty, flexible.GetObservable(FlexibleWindow.ShowTitleProperty));
        host.Bind(AdvancedWindow.CustomIconProperty, flexible.GetObservable(FlexibleWindow.CustomIconProperty));
        host.Bind(AdvancedWindow.TitleBarContentProperty, flexible.GetObservable(FlexibleWindow.TitleBarContentProperty));
        host.Bind(AdvancedWindow.BottomContentProperty, flexible.GetObservable(FlexibleWindow.BottomContentProperty));
            
        host.Bind(Window.WindowStartupLocationProperty, flexible.GetObservable(FlexibleWindow.WindowStartupLocationProperty));
        host.Bind(Window.IconProperty, flexible.GetObservable(FlexibleWindow.IconProperty));
        host.Bind(Window.TitleProperty, flexible.GetObservable(FlexibleWindow.TitleProperty));
        host.Bind(Window.SizeToContentProperty, flexible.GetObservable(FlexibleWindow.SizeToContentProperty));
            
        //host.Bind(TopLevel.TransparencyLevelHintProperty, flexible.GetObservable(FlexibleWindow.TransparencyLevelHintProperty));
            host.Bind(TemplatedControl.BackgroundProperty,
                flexible.GetObservable(FlexibleWindow.WindowBackgroundProperty).Where(x => x is not null));
        
        host.Height = flexible.PrefHeight;
        host.Width = flexible.PrefWidth;

        host.ExtendClientAreaToDecorationsHint = true;
        
        host.Content = flexible;

        return host;
    }
}