using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class LoginWindow : FlexibleWindow
    {
        public LoginWindow()
        {
            DataContext = new LoginWindowViewModel();

            InitializeComponent();
        }


    }
}