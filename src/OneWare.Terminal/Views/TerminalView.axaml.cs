using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using OneWare.Terminal.ViewModels;

namespace OneWare.Terminal.Views
{
    public partial class TerminalView : UserControl
    {
        public TerminalView()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            if (DataContext is TerminalViewModel tvm)
            {
                DataContext_Changed(this, EventArgs.Empty);
            }
            else
            {
                DataContextChanged -= DataContext_Changed;
                DataContextChanged += DataContext_Changed;
            }
        }

        private void DataContext_Changed(object? sender, EventArgs e)
        {
            if (DataContext is TerminalViewModel tvm) tvm.StartCreate();
        }
    }
}