using Avalonia.Controls;
using OneWare.CloudIntegration.ViewModels;

namespace OneWare.CloudIntegration.Views;

public partial class OneWareCloudAccountFlyoutView : UserControl
{
    public OneWareCloudAccountFlyoutView()
    {
        InitializeComponent();
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        // The active organization can be switched on the web; refresh whenever the flyout is shown.
        if (DataContext is OneWareCloudAccountFlyoutViewModel viewModel)
            _ = viewModel.RefreshAsync();
    }
}
