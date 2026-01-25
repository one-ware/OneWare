using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OneWare.Core.ViewModels.Windows;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;

namespace OneWare.Core.Views.Windows;

public partial class MessageBoxWindow : FlexibleWindow
{
    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    public MessageBoxWindow(MessageBoxRequest request) : this()
    {
        Title = request.Title;
        DataContext = new MessageBoxViewModel(request);

        ApplyIcon(request.Icon);

        KeyDown += (_, e) =>
        {
            if (DataContext is not MessageBoxViewModel vm)
                return;

            if (e.Key == Key.Enter)
            {
                if (vm.TryExecuteDefault())
                    e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (vm.TryExecuteCancel())
                    e.Handled = true;
            }
        };

        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is not MessageBoxViewModel vm)
                return;

            vm.AttachWindow(this);

            if (vm.ShowInput)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    InputBox.SelectAll();
                    InputBox.Focus();
                });
            }
            else
            {
                Dispatcher.UIThread.Post(() => Focus());
            }
        };

        Closed += (_, _) =>
        {
            if (DataContext is MessageBoxViewModel vm)
                vm.EnsureCanceled();
        };
    }

    public MessageBoxWindow(string title, string message, MessageBoxMode mode = MessageBoxMode.AllButtons,
        MessageBoxIcon defaultIcon = MessageBoxIcon.Warning) : this(BuildRequest(title, message, mode, defaultIcon))
    {
    }

    public MessageBoxResult Result => (DataContext as MessageBoxViewModel)!.Result;

    public MessageBoxStatus BoxStatus => Result.Status;

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

    private void ApplyIcon(MessageBoxIcon icon)
    {
        if (Application.Current is null) throw new NullReferenceException(nameof(Application.Current));

        switch (icon)
        {
            case MessageBoxIcon.Info:
                SetIcon(
                    "avares://OneWare.Core/Assets/Images/Icons/Hint_Icon.png",
                    "VsImageLib.StatusInformation16X");
                break;
            case MessageBoxIcon.Error:
                SetIcon(
                    "avares://OneWare.Core/Assets/Images/Icons/Error_Icon.png",
                    "VsImageLib.StatusCriticalError16X");
                break;
            default:
                SetIcon(
                    "avares://OneWare.Core/Assets/Images/Icons/Warning_Icon.png",
                    "VsImageLib.StatusWarning16X");
                break;
        }
    }

    private void SetIcon(string windowIconPath, string resourceKey)
    {
        var icon = (Bitmap?)SharedConverters.PathToBitmapConverter.Convert(
            windowIconPath, typeof(Bitmap), null, null);
        if (icon != null)
            Icon = new WindowIcon(icon);

        if (Application.Current is not null)
            CustomIcon = (IImage?)Application.Current.FindResource(Application.Current.RequestedThemeVariant,
                resourceKey);
    }

    private static MessageBoxRequest BuildRequest(string title, string message, MessageBoxMode mode,
        MessageBoxIcon icon)
    {
        var request = new MessageBoxRequest
        {
            Title = title,
            Message = message,
            Icon = icon
        };

        switch (mode)
        {
            case MessageBoxMode.NoCancel:
                request.Buttons = new[]
                {
                    CreateYesButton(),
                    CreateNoButton()
                };
                break;
            case MessageBoxMode.OnlyOk:
                request.Buttons = new[]
                {
                    CreateOkButton()
                };
                break;
            case MessageBoxMode.Input:
                request.Buttons = new[]
                {
                    CreateOkButton(),
                    CreateCancelButton()
                };
                request.Input = new MessageBoxInputOptions();
                break;
            case MessageBoxMode.PasswordInput:
                request.Buttons = new[]
                {
                    CreateOkButton(),
                    CreateCancelButton()
                };
                request.Input = new MessageBoxInputOptions { Kind = MessageBoxInputKind.Password };
                break;
            case MessageBoxMode.SelectFolder:
                request.Buttons = new[]
                {
                    CreateOkButton(),
                    CreateCancelButton()
                };
                request.Input = new MessageBoxInputOptions { ShowFolderButton = true };
                break;
            case MessageBoxMode.SelectItem:
                request.Buttons = new[]
                {
                    CreateOkButton(),
                    CreateCancelButton()
                };
                break;
            default:
                request.Buttons = new[]
                {
                    CreateYesButton(),
                    CreateNoButton(),
                    CreateCancelButton()
                };
                break;
        }

        return request;
    }

    private static MessageBoxButton CreateYesButton()
    {
        return new MessageBoxButton
        {
            Text = "Yes",
            Role = MessageBoxButtonRole.Yes,
            Style = MessageBoxButtonStyle.Primary,
            IsDefault = true
        };
    }

    private static MessageBoxButton CreateNoButton()
    {
        return new MessageBoxButton
        {
            Text = "No",
            Role = MessageBoxButtonRole.No,
            Style = MessageBoxButtonStyle.Secondary
        };
    }

    private static MessageBoxButton CreateCancelButton()
    {
        return new MessageBoxButton
        {
            Text = "Cancel",
            Role = MessageBoxButtonRole.Cancel,
            Style = MessageBoxButtonStyle.Secondary,
        };
    }

    private static MessageBoxButton CreateOkButton()
    {
        return new MessageBoxButton
        {
            Text = "Ok",
            Role = MessageBoxButtonRole.Yes,
            Style = MessageBoxButtonStyle.Primary,
            IsDefault = true,
        };
    }
}
