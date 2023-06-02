using Avalonia;
using OneWare.Shared;

namespace OneWare.Settings.Views
{
    public partial class SettingsWindow : AdvancedWindow
    {
        public static SettingsWindow? LastInstance;

        public SettingsWindow()
        {
            LastInstance = this;
            
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }
    }
}