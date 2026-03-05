using Avalonia.Interactivity;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Controls;

namespace OneWare.CloudIntegration.Views;

public partial class AuthenticateCloudView : FlexibleWindow
{
    public AuthenticateCloudView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AuthenticateCloudViewModel viewModel)
        {
            await viewModel.LoginAsync(this);
        }
    }
}