using OneWare.Essentials.Controls;
using OneWare.Essentials.Services; // Assuming IDockService is defined in this namespace

namespace OneWare.PackageManager.Views
{
    public partial class PackageManagerView : FlexibleWindow
    {
        public PackageManagerView(IDockService dockService) : base(dockService)
        {
            InitializeComponent();
            // Additional initialization code can go here
        }
    }
}
