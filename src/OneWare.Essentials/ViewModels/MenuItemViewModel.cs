using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.ViewModels
{
    public class MenuItemViewModel(string partId) : ObservableObject, ICanHaveObservableItems<MenuItemViewModel>, ICanHaveIcon
    {
        public string PartId { get; } = partId;
        public int Priority { get; init; }
        
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

        private IImage? _icon;
        public IImage? Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private IDisposable? _subscription;
        public IObservable<object?>? IconObservable
        {
            set
            {
                _subscription?.Dispose();
                _subscription = value?.Subscribe(x => Icon = x as IImage);
            }
        }
        
        private KeyGesture? _inputGesture;
        public KeyGesture? InputGesture
        {
            get => _inputGesture;
            set => SetProperty(ref _inputGesture, value);
        }
        
        private ObservableCollection<MenuItemViewModel>? _items;
        public ObservableCollection<MenuItemViewModel>? Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public string Name => Header ?? string.Empty;
    }
}