using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OneWare.Shared.Converters;
using OneWare.Shared.ViewModels;

namespace OneWare.Shared.Views
{
    public partial class MessageBoxWindow : AdvancedWindow
    {
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        public MessageBoxWindow(string title, string message, MessageBoxMode mode = MessageBoxMode.AllButtons, MessageBoxIcon defaultIcon = MessageBoxIcon.Warning) : this()
        {
            DataContext = new MessageBoxViewModel(title, message, mode);
            
            switch (defaultIcon) //Warning is default icon defined in xaml
            {
                case MessageBoxIcon.Info:
                    var infoIcon = (Bitmap?)SharedConverters.PathToBitmapConverter.Convert(
                        "avares://OneWare.Core/Assets/Images/Icons/Hint_Icon.png", typeof(IBitmap), null, null);
                    if(infoIcon != null) Icon = new WindowIcon(infoIcon);
                    CustomIcon = (IImage?)Application.Current!.FindResource("VsImageLib.StatusInformation16X");
                    break;

                case MessageBoxIcon.Error:
                    var errorIcon = (Bitmap?)SharedConverters.PathToBitmapConverter.Convert(
                        "avares://OneWare.Core/Assets/Images/Icons/Error_Icon.png", typeof(IBitmap), null, null);
                    if(errorIcon != null) Icon = new WindowIcon(errorIcon);
                    CustomIcon = (IImage?)Application.Current!.FindResource("VsImageLib.StatusCriticalError16X");
                    break;
                default:
                    var warningIcon = (Bitmap?)SharedConverters.PathToBitmapConverter.Convert(
                        "avares://OneWare.Core/Assets/Images/Icons/Warning_Icon.png", typeof(IBitmap), null, null);
                    if(warningIcon != null) Icon = new WindowIcon(warningIcon);
                    CustomIcon = (IImage?)Application.Current!.FindResource("VsImageLib.StatusWarning16X");
                    break;
            }

            if (mode == MessageBoxMode.Input || mode == MessageBoxMode.PasswordInput)
            {
                KeyDown += (o, i) =>
                {
                    if (i.Key == Key.Enter && DataContext is MessageBoxViewModel mb)
                    {
                        mb.BoxStatus = MessageBoxStatus.Yes;
                        Close();
                    }
                };

                Opened += (o, i) => { InputBox.Focus(); };
            }
            else
            {
                KeyDown += (o, i) =>
                {
                    if (i.Key == Key.Enter && DataContext is MessageBoxViewModel mb)
                    {
                        mb.BoxStatus = MessageBoxStatus.Yes;
                        Close();
                    }
                };
            }
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
}