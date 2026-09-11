using Avalonia;
using OneWare.Essentials.Controls;
using OneWare.PackageManager.ViewModels;

namespace OneWare.PackageManager.Views;

public partial class ConfigurationProfileImportView : FlexibleWindow
{
    public ConfigurationProfileImportView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is ConfigurationProfileImportViewModel vm)
            _ = vm.StartAsync();
    }
}