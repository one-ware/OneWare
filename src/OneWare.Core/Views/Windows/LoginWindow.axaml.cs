using Avalonia;
using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class LoginWindow : AdvancedWindow
    {
        public LoginWindow()
        {
            DataContext = new LoginWindowViewModel();

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }


    }
}