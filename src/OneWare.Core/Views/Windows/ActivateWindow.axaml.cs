using Avalonia;
using OneWare.Core.ViewModels.Windows;
using Prism.Ioc;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    
    public partial class ActivateWindow : AdvancedWindow
    {
        public static ActivateWindow? LastInstance { get; private set; }
        
        public ActivateWindow()
        {
            LastInstance = this;
            DataContext = ContainerLocator.Container.Resolve<ActivateWindowViewModel>();

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }
    }
}