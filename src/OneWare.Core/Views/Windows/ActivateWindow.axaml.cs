using OneWare.Core.ViewModels.Windows;
using OneWare.Essentials.Controls;
using Autofac;  // Import Autofac for Dependency Injection

namespace OneWare.Core.Views.Windows
{
    public partial class ActivateWindow : FlexibleWindow
    {
        // Constructor with Autofac Dependency Injection
        public ActivateWindow(ActivateWindowViewModel viewModel)
        {
            // Set the DataContext to the injected ViewModel
            DataContext = viewModel;

            // Initialize the window components
            InitializeComponent();
        }
    }
}
