using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Services;

namespace OneWare.SourceControl.Views;

public partial class AuthenticateCloudView : FlexibleWindow
{
    // Modify the constructor to accept IDockService
    public AuthenticateCloudView(IDockService dockService) : base(dockService) // <--- Pass dockService to the base constructor
    {
        InitializeComponent();
    }
}