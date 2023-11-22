using OneWare.Core.ViewModels.Windows;
using Prism.Ioc;
using OneWare.SDK.Controls;

namespace OneWare.Core.Views.Windows
{
    public partial class ActivateWindow : FlexibleWindow
    {
        public ActivateWindow()
        {
            DataContext = ContainerLocator.Container.Resolve<ActivateWindowViewModel>();

            InitializeComponent();
        }
    }
}