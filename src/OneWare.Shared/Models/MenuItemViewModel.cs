using System.Windows.Input;
using Avalonia.Input;
using OneWare.Shared.ViewModels;

namespace OneWare.Shared.Models
{
    public class MenuItemViewModel : ViewModelBase
    {
        public int Priority { get; set; }
        
        private ICommand? _command;
        public ICommand? Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }
        
        private object? _commandParameter;
        public object? CommandParameter
        {
            get => _commandParameter;
            set => SetProperty(ref _commandParameter, value);
        }
        
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private string? _header;
        public string? Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        private object? _icon;
        public object? Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private KeyGesture? _hotkey;
        public KeyGesture? Hotkey
        {
            get => _hotkey;
            set => SetProperty(ref _hotkey, value);
        }
        
        private IList<MenuItemViewModel>? _items;
        public IList<MenuItemViewModel>? Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }
    }
}