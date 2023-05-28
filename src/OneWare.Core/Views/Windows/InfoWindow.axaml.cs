using Avalonia;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class InfoWindow : AdvancedWindow
    {
        public InfoWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }


    }
}