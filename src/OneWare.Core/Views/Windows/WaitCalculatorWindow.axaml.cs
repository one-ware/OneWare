using Avalonia;
using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class WaitCalculatorWindow : AdvancedWindow
    {
        private readonly WaitCalculatorWindowViewModel _windowViewModel;

        public WaitCalculatorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            _windowViewModel = new WaitCalculatorWindowViewModel();
            DataContext = _windowViewModel;
        }


    }
}