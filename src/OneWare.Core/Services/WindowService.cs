using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
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

    public async Task<MessageBoxStatus> ShowProceedWarningAsync(string message, Window? owner = null)
    {
        var msg = new MessageBoxWindow("Warning", message, MessageBoxMode.NoCancel);
        await ShowDialogAsync(msg, owner);
        return msg.BoxStatus;
    }

    public void ShowNotification(string title, string message)
    {
        throw new System.NotImplementedException();
    }

    public void ShowNotificationWithButton(string title, string message, string buttonText, Action buttonAction, IImage? icon = null)
    {
        var model = new CustomNotificationViewModel
        {
            Title = title,
            Message = message,
            ButtonText = buttonText, Image = icon
        };
        model.OnButtonClick = buttonAction;
        
        ContainerLocator.Container.Resolve<MainWindow>().NotificationManager.Show(model);
    }
}