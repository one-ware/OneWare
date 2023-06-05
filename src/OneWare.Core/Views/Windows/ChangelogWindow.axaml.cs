using Avalonia;
using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class ChangelogWindow : FlexibleWindow
    {
        public ChangelogWindow()
        {
            DataContext = new ChangelogWindowViewModel();

            InitializeComponent();
        }
    }
}