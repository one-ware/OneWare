using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;


namespace OneWare.SDK.Models
{
    public class ButtonModel : ObservableObject
    {
        private string? _header;
        public string? Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        private ICommand? _command;
        public ICommand? Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }

        private IBrush? _backgroundBrush;
        public IBrush? BackgroundBrush
        {
            get => _backgroundBrush;
            set => SetProperty(ref _backgroundBrush, value);
        }

        private object? _commandParameter;
        public object? CommandParameter
        {
            get => _commandParameter;
            set => SetProperty(ref _commandParameter, value);
        }
    }
}