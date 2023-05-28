using Avalonia;
using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class ChangelogWindow : AdvancedWindow
    {
        public ChangelogWindow()
        {
            DataContext = new ChangelogWindowViewModel();

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }
    }
}