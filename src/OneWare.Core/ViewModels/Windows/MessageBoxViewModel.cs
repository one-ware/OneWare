using System.Collections.ObjectModel;
using Avalonia.Controls;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public enum MessageBoxMode
{
    AllButtons,
    NoCancel,
    OnlyOk,
    Input,
    PasswordInput,
    SelectFolder,
    SelectItem
}

public class MessageBoxViewModel : FlexibleWindowViewModelBase
{
    private string? _input;

    private object? _selectedItem;

    private ObservableCollection<object> _selectionItems = new();

    public MessageBoxViewModel(string title, string message, MessageBoxMode mode)
    {
        Title = title;
        Message = message;
        Id = "MessageBox...";

        //TODO USE FLAGS

        if (mode == MessageBoxMode.PasswordInput) PasswordChar = "*";
        else if (mode == MessageBoxMode.SelectFolder) ShowFolderButton = true;

        switch (mode)
        {
            case MessageBoxMode.NoCancel:
                ShowCancel = false;
                break;

            case MessageBoxMode.Input:
            case MessageBoxMode.PasswordInput:
            case MessageBoxMode.SelectFolder:
                ShowInput = true;
                ShowOk = true;
                ShowYes = false;
                ShowNo = false;
                break;

            case MessageBoxMode.OnlyOk:
                ShowInput = false;
                ShowOk = true;
                ShowYes = false;
                ShowNo = false;
                ShowCancel = false;
                break;

            case MessageBoxMode.SelectItem:
                ShowSelection = true;
                ShowOk = true;
                ShowYes = false;
                ShowNo = false;
                break;
        }
    }

    public string? Input
    {
        get => _input;
        set => SetProperty(ref _input, value);
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public ObservableCollection<object> SelectionItems
    {
        get => _selectionItems;
        set => SetProperty(ref _selectionItems, value);
    }

    public MessageBoxStatus BoxStatus { get; set; } = MessageBoxStatus.Canceled;
    public string Message { get; }
    public bool ShowYes { get; } = true;
    public bool ShowNo { get; } = true;
    public bool ShowCancel { get; } = true;
    public bool ShowOk { get; }
    public bool ShowInput { get; }
    public bool ShowFolderButton { get; }
    public bool ShowSelection { get; }
    public string PasswordChar { get; } = "";

    public void No(FlexibleWindow window)
    {
        BoxStatus = MessageBoxStatus.No;
        window.Close();
    }

    public void Yes(FlexibleWindow window)
    {
        BoxStatus = MessageBoxStatus.Yes;
        window.Close();
    }

    public void Cancel(FlexibleWindow window)
    {
        BoxStatus = MessageBoxStatus.Canceled;
        window.Close();
    }

    public async Task SelectPathAsync(FlexibleWindow window)
    {
        if (window.Host == null) throw new NullReferenceException(nameof(TopLevel));

        var folder = await StorageProviderHelper.SelectFolderAsync(window.Host, "Select Directory", Input);

        Input = folder ?? "";
    }
}