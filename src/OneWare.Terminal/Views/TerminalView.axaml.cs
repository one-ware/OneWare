using Avalonia.Controls;
using OneWare.Terminal.ViewModels;

namespace OneWare.Terminal.Views
{
    public partial class TerminalView : UserControl
    {
        public TerminalView()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            
            if (DataContext is TerminalViewModel tvm)
            {
                tvm.StartCreate();
            }
        }
    }
}