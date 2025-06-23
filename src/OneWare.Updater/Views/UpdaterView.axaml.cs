using OneWare.Essentials.Controls;
using OneWare.Essentials.Services;

namespace OneWare.Updater.Views;

public partial class UpdaterView : FlexibleWindow
{
    public UpdaterView(IDockService dockService)
           : base(dockService)
    {
        InitializeComponent();
        // Additional initialization code can go here
    }
}