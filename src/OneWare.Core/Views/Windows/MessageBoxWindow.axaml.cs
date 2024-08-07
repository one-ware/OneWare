﻿using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OneWare.Core.ViewModels.Windows;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Enums;

namespace OneWare.Core.Views.Windows;

public partial class MessageBoxWindow : FlexibleWindow
{
    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    public MessageBoxWindow(string title, string message, MessageBoxMode mode = MessageBoxMode.AllButtons,
        MessageBoxIcon defaultIcon = MessageBoxIcon.Warning) : this()
    {
        Title = title;
        DataContext = new MessageBoxViewModel(title, message, mode);

        if (Application.Current is null) throw new NullReferenceException(nameof(Application.Current));
        switch (defaultIcon) //Warning is default icon defined in xaml
        {
            case MessageBoxIcon.Info:
                var infoIcon = (Bitmap?)SharedConverters.PathToBitmapConverter.Convert(
                    "avares://OneWare.Core/Assets/Images/Icons/Hint_Icon.png", typeof(Bitmap), null, null);
                if (infoIcon != null) Icon = new WindowIcon(infoIcon);
                CustomIcon = (IImage?)Application.Current.FindResource(Application.Current.RequestedThemeVariant,
                    "VsImageLib.StatusInformation16X");
                break;

            case MessageBoxIcon.Error:
                var errorIcon = (Bitmap?)SharedConverters.PathToBitmapConverter.Convert(
                    "avares://OneWare.Core/Assets/Images/Icons/Error_Icon.png", typeof(Bitmap), null, null);
                if (errorIcon != null) Icon = new WindowIcon(errorIcon);
                CustomIcon = (IImage?)Application.Current.FindResource(Application.Current.RequestedThemeVariant,
                    "VsImageLib.StatusCriticalError16X");
                break;
            default:
                var warningIcon = (Bitmap?)SharedConverters.PathToBitmapConverter.Convert(
                    "avares://OneWare.Core/Assets/Images/Icons/Warning_Icon.png", typeof(Bitmap), null, null);
                if (warningIcon != null) Icon = new WindowIcon(warningIcon);
                CustomIcon = (IImage?)Application.Current.FindResource(Application.Current.RequestedThemeVariant,
                    "VsImageLib.StatusWarning16X");
                break;
        }

        if (mode is MessageBoxMode.Input or MessageBoxMode.PasswordInput)
        {
            KeyDown += (o, i) =>
            {
                if (i.Key == Key.Enter && DataContext is MessageBoxViewModel mb)
                {
                    mb.BoxStatus = MessageBoxStatus.Yes;
                    i.Handled = true;
                    Close();
                }
            };
        }
        else
        {
            KeyDown += (o, i) =>
            {
                if (i.Key == Key.Enter && DataContext is MessageBoxViewModel mb)
                {
                    mb.BoxStatus = MessageBoxStatus.Yes;
                    i.Handled = true;
                    Close();
                }
            };
        }
        
        AttachedToVisualTree += (_, _) =>
        {
            if (mode == MessageBoxMode.Input)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    InputBox.SelectAll();
                    InputBox.Focus();
                });
            }
            else
            {
                Dispatcher.UIThread.Post(() => this.Focus());
            }
        };
    }

    public MessageBoxStatus BoxStatus => (DataContext as MessageBoxViewModel)!.BoxStatus;

    public string? Input
    {
        get => (DataContext as MessageBoxViewModel)!.Input;
        set => (DataContext as MessageBoxViewModel)!.Input = value;
    }

    public ObservableCollection<object> SelectionItems => (DataContext as MessageBoxViewModel)!.SelectionItems;

    public object? SelectedItem
    {
        get => (DataContext as MessageBoxViewModel)!.SelectedItem;
        set => (DataContext as MessageBoxViewModel)!.SelectedItem = value;
    }
}