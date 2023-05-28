using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;

using OneWare.Settings.Models;
using OneWare.Settings.ViewModels;
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