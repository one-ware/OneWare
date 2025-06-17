using OneWare.Core.ViewModels.Windows;
using OneWare.Essentials.Controls;

namespace OneWare.Core.Views.Windows;

public partial class ActivateWindow : FlexibleWindow
{
    public ActivateWindow(ActivateWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
