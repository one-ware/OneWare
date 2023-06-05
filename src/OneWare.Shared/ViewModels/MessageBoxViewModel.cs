using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;


namespace OneWare.Shared.ViewModels
{
    public enum MessageBoxStatus
    {
        Canceled,
        Yes,
        No
    }

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

    public enum MessageBoxIcon
    {
        Info,
        Warning,
        Error
    }

    public class MessageBoxViewModel : FlexibleWindowViewModelBase
    {
        private string? _input;
        public string? Input
        {
            get => _input;
            set => SetProperty(ref _input, value);
        }

        private object? _selectedItem;
        public object? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        private ObservableCollection<object> _selectionItems = new();
        public ObservableCollection<object> SelectionItems
        {
            get => _selectionItems;
            set => SetProperty(ref _selectionItems, value);
        }
        
        public MessageBoxStatus BoxStatus { get; set; } = MessageBoxStatus.Canceled;
        public string Title { get; }
        public string Message { get; }
        public bool ShowYes { get; } = true;
        public bool ShowNo { get; } = true;
        public bool ShowCancel { get; } = true;
        public bool ShowOk { get; }
        public bool ShowInput { get; }
        public bool ShowFolderButton { get; }
        public bool ShowSelection { get; }

        public string PasswordChar { get; } = "";
        
        public MessageBoxViewModel(string title, string message, MessageBoxMode mode)
        {
            Title = title;
            Message = message;

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

        public void No(Window window)
        {
            BoxStatus = MessageBoxStatus.No;
            window.Close();
        }

        public void Yes(Window window)
        {
            BoxStatus = MessageBoxStatus.Yes;
            window.Close();
        }

        public void Cancel(Window window)
        {
            BoxStatus = MessageBoxStatus.Canceled;
            window.Close();
        }
        
        public async Task SelectPathAsync(Window window)
        {
            var folder = await Tools.SelectFolderAsync(window, "Select Directory", Input);
            
            Input = folder ?? "";
        }
    }
}